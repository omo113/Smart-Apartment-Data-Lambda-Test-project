output "api_base_url" {
  description = "Base URL for the HTTP API default stage."
  value       = aws_apigatewayv2_api.http.api_endpoint
}

output "geocode_invoke_url" {
  description = "Exact GET endpoint for the geocode route."
  value       = "${aws_apigatewayv2_api.http.api_endpoint}/Geocode"
}

output "geocode_postman_example_url" {
  description = "Example URL you can paste into Postman for a sample request."
  value       = "${aws_apigatewayv2_api.http.api_endpoint}/Geocode?address=${urlencode("70 Vanderbilt Ave, New York, NY 10017, United States")}"
}

output "lambda_function_name" {
  description = "Lambda function name for the geocode service."
  value       = aws_lambda_function.geocode.function_name
}

output "dynamodb_table_name" {
  description = "DynamoDB table name that stores geocode cache entries."
  value       = aws_dynamodb_table.geocode_cache.name
}

output "secret_name" {
  description = "Secrets Manager secret name that stores the Google API key."
  value       = aws_secretsmanager_secret.google_api_key.name
}
