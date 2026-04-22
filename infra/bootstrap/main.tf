provider "aws" {
  region = var.aws_region

  default_tags {
    tags = merge(
      {
        Project     = "smart-apartment-geocode"
        Environment = var.environment
        ManagedBy   = "Terraform"
        Stack       = "bootstrap"
      },
      var.tags
    )
  }
}

data "aws_caller_identity" "current" {}

data "aws_region" "current" {}

locals {
  state_bucket_name        = "smart-apartment-tfstate-${var.aws_account_id}-${var.aws_region}"
  state_key_prefix         = "${var.environment}/"
  lock_table_name          = "smart-apartment-tf-locks-${var.environment}"
  github_actions_role_name = "github-actions-terraform-smart-apartment-${var.environment}"
  github_subject           = "repo:${var.github_repository}:ref:refs/heads/${var.github_branch}"
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

resource "aws_s3_bucket" "terraform_state" {
  bucket = local.state_bucket_name
}

resource "aws_s3_bucket_versioning" "terraform_state" {
  bucket = aws_s3_bucket.terraform_state.id

  versioning_configuration {
    status = "Enabled"
  }
}

resource "aws_s3_bucket_server_side_encryption_configuration" "terraform_state" {
  bucket = aws_s3_bucket.terraform_state.id

  rule {
    apply_server_side_encryption_by_default {
      sse_algorithm = "AES256"
    }
  }
}

resource "aws_s3_bucket_public_access_block" "terraform_state" {
  bucket = aws_s3_bucket.terraform_state.id

  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

resource "aws_dynamodb_table" "terraform_lock" {
  name         = local.lock_table_name
  billing_mode = "PAY_PER_REQUEST"
  hash_key     = "LockID"

  attribute {
    name = "LockID"
    type = "S"
  }
}

resource "aws_iam_openid_connect_provider" "github_actions" {
  url = "https://token.actions.githubusercontent.com"

  client_id_list = [
    "sts.amazonaws.com"
  ]
}

data "aws_iam_policy_document" "github_actions_assume_role" {
  statement {
    effect = "Allow"

    principals {
      type        = "Federated"
      identifiers = [aws_iam_openid_connect_provider.github_actions.arn]
    }

    actions = [
      "sts:AssumeRoleWithWebIdentity"
    ]

    condition {
      test     = "StringEquals"
      variable = "token.actions.githubusercontent.com:aud"
      values   = ["sts.amazonaws.com"]
    }

    condition {
      test     = "StringEquals"
      variable = "token.actions.githubusercontent.com:sub"
      values   = [local.github_subject]
    }
  }
}

resource "aws_iam_role" "github_actions_terraform" {
  name                 = local.github_actions_role_name
  assume_role_policy   = data.aws_iam_policy_document.github_actions_assume_role.json
  description          = "Allows GitHub Actions to manage the Smart Apartment demo infrastructure."
  permissions_boundary = var.permissions_boundary_arn
}

data "aws_iam_policy_document" "github_actions_permissions" {
  statement {
    sid    = "TerraformStateBucketList"
    effect = "Allow"

    actions = [
      "s3:GetBucketLocation",
      "s3:ListBucket"
    ]

    resources = [
      aws_s3_bucket.terraform_state.arn
    ]

    condition {
      test     = "StringLike"
      variable = "s3:prefix"
      values   = ["${local.state_key_prefix}*"]
    }
  }

  statement {
    sid    = "TerraformStateObjectAccess"
    effect = "Allow"

    actions = [
      "s3:GetObject",
      "s3:PutObject",
      "s3:DeleteObject"
    ]

    resources = [
      "${aws_s3_bucket.terraform_state.arn}/${local.state_key_prefix}*"
    ]
  }

  statement {
    sid    = "TerraformStateLockAccess"
    effect = "Allow"

    actions = [
      "dynamodb:DeleteItem",
      "dynamodb:DescribeTable",
      "dynamodb:GetItem",
      "dynamodb:PutItem"
    ]

    resources = [
      aws_dynamodb_table.terraform_lock.arn
    ]
  }

  statement {
    sid    = "ManageDemoInfrastructure"
    effect = "Allow"

    actions = [
      "apigateway:*",
      "dynamodb:*",
      "lambda:*",
      "logs:*",
      "secretsmanager:*",
      "iam:CreateRole",
      "iam:DeleteRole",
      "iam:DeleteRolePolicy",
      "iam:DeleteRolePermissionsBoundary",
      "iam:GetRole",
      "iam:GetRolePolicy",
      "iam:ListAttachedRolePolicies",
      "iam:ListRolePolicies",
      "iam:ListRoleTags",
      "iam:PassRole",
      "iam:PutRolePermissionsBoundary",
      "iam:PutRolePolicy",
      "iam:TagRole",
      "iam:UntagRole",
      "iam:UpdateAssumeRolePolicy"
    ]

    resources = ["*"]
  }
}

resource "aws_iam_role_policy" "github_actions_permissions" {
  name   = "smart-apartment-demo-terraform"
  role   = aws_iam_role.github_actions_terraform.id
  policy = data.aws_iam_policy_document.github_actions_permissions.json
}
