provider "aws" {
  region = var.aws_region

  default_tags {
    tags = merge(
      {
        Project     = "smart-apartment-geocode"
        Environment = var.environment
        ManagedBy   = "Terraform"
        Stack       = "demo"
      },
      var.tags
    )
  }
}

data "aws_caller_identity" "current" {}

data "aws_region" "current" {}

locals {
  lambda_function_name = "smart-apartment-geocode-${var.environment}"
  lambda_role_name     = "${local.lambda_function_name}-lambda-role"
  log_group_name       = "/aws/lambda/${local.lambda_function_name}"
  api_log_group_name   = "/aws/apigateway/${local.lambda_function_name}-http"
  dynamodb_table_name  = "smart-apartment-geocode-cache-${var.environment}"
  secret_name          = "smart-apartment/google-geocoding/${var.environment}"
  http_api_name        = "${local.lambda_function_name}-http"
}

check "expected_account" {
  assert {
    condition     = data.aws_caller_identity.current.account_id == var.aws_account_id
    error_message = "Connected to AWS account ${data.aws_caller_identity.current.account_id}, expected ${var.aws_account_id}."
  }
}

check "expected_region" {
  assert {
    condition     = data.aws_region.current.region == var.aws_region
    error_message = "Connected to AWS region ${data.aws_region.current.region}, expected ${var.aws_region}."
  }
}

resource "aws_cloudwatch_log_group" "lambda" {
  name              = local.log_group_name
  retention_in_days = 14
}

resource "aws_cloudwatch_log_group" "api_gateway" {
  name              = local.api_log_group_name
  retention_in_days = 14
}

resource "aws_dynamodb_table" "geocode_cache" {
  name         = local.dynamodb_table_name
  billing_mode = "PAY_PER_REQUEST"
  hash_key     = "NormalizedAddress"

  attribute {
    name = "NormalizedAddress"
    type = "S"
  }

  ttl {
    attribute_name = "TtlEpochSeconds"
    enabled        = true
  }
}

resource "aws_secretsmanager_secret" "google_api_key" {
  name        = local.secret_name
  description = "Google Geocoding API key for the Smart Apartment demo Lambda."
}

data "aws_iam_policy_document" "lambda_assume_role" {
  statement {
    effect = "Allow"

    principals {
      type        = "Service"
      identifiers = ["lambda.amazonaws.com"]
    }

    actions = ["sts:AssumeRole"]
  }
}

resource "aws_iam_role" "lambda_execution" {
  name                 = local.lambda_role_name
  assume_role_policy   = data.aws_iam_policy_document.lambda_assume_role.json
  description          = "Execution role for the Smart Apartment geocode demo Lambda."
  permissions_boundary = var.permissions_boundary_arn
}

data "aws_iam_policy_document" "lambda_execution" {
  statement {
    sid    = "WriteFunctionLogs"
    effect = "Allow"

    actions = [
      "logs:CreateLogStream",
      "logs:PutLogEvents"
    ]

    resources = [
      "${aws_cloudwatch_log_group.lambda.arn}:*"
    ]
  }

  statement {
    sid    = "AccessGeocodeCache"
    effect = "Allow"

    actions = [
      "dynamodb:GetItem",
      "dynamodb:PutItem"
    ]

    resources = [
      aws_dynamodb_table.geocode_cache.arn
    ]
  }

  statement {
    sid    = "ReadGoogleApiKey"
    effect = "Allow"

    actions = [
      "secretsmanager:GetSecretValue"
    ]

    resources = [
      aws_secretsmanager_secret.google_api_key.arn
    ]
  }
}

resource "aws_iam_role_policy" "lambda_execution" {
  name   = "${local.lambda_function_name}-execution"
  role   = aws_iam_role.lambda_execution.id
  policy = data.aws_iam_policy_document.lambda_execution.json
}

resource "aws_lambda_function" "geocode" {
  function_name    = local.lambda_function_name
  description      = "Geocodes addresses through Google Maps and caches stable responses in DynamoDB."
  role             = aws_iam_role.lambda_execution.arn
  runtime          = "dotnet8"
  handler          = "SmartApartmentLambda.Service"
  filename         = var.lambda_package_path
  source_code_hash = var.lambda_package_hash
  memory_size      = var.lambda_memory_size
  timeout          = var.lambda_timeout_seconds
  architectures    = ["x86_64"]

  environment {
    variables = {
      "GeocodeCache__TableName"              = aws_dynamodb_table.geocode_cache.name
      "GeocodeCache__CacheDurationDays"      = tostring(var.cache_duration_days)
      "GoogleGeocoding__ApiKeySecretName"    = aws_secretsmanager_secret.google_api_key.name
      "GoogleGeocoding__ApiKeySecretJsonKey" = var.google_api_key_secret_json_key
    }
  }

  depends_on = [
    aws_cloudwatch_log_group.lambda
  ]
}

resource "aws_apigatewayv2_api" "http" {
  name          = local.http_api_name
  protocol_type = "HTTP"
}

resource "aws_apigatewayv2_integration" "lambda" {
  api_id                 = aws_apigatewayv2_api.http.id
  integration_type       = "AWS_PROXY"
  integration_method     = "POST"
  integration_uri        = aws_lambda_function.geocode.invoke_arn
  payload_format_version = "1.0"
  timeout_milliseconds   = var.lambda_timeout_seconds * 1000
}

resource "aws_apigatewayv2_route" "geocode" {
  api_id    = aws_apigatewayv2_api.http.id
  route_key = "GET /Geocode"
  target    = "integrations/${aws_apigatewayv2_integration.lambda.id}"
}

resource "aws_apigatewayv2_stage" "default" {
  api_id      = aws_apigatewayv2_api.http.id
  name        = "$default"
  auto_deploy = true

  access_log_settings {
    destination_arn = aws_cloudwatch_log_group.api_gateway.arn
    format = jsonencode({
      requestId      = "$context.requestId"
      routeKey       = "$context.routeKey"
      status         = "$context.status"
      integration    = "$context.integration.status"
      responseLength = "$context.responseLength"
      errorMessage   = "$context.error.message"
    })
  }
}

resource "aws_lambda_permission" "allow_api_gateway" {
  statement_id  = "AllowHttpApiInvokeGeocode"
  action        = "lambda:InvokeFunction"
  function_name = aws_lambda_function.geocode.function_name
  principal     = "apigateway.amazonaws.com"
  source_arn    = "${aws_apigatewayv2_api.http.execution_arn}/*/GET/Geocode"
}
