variable "aws_account_id" {
  description = "AWS account id that owns the demo infrastructure."
  type        = string
}

variable "aws_region" {
  description = "AWS region that hosts the demo infrastructure."
  type        = string
}

variable "environment" {
  description = "Environment name used in resource naming."
  type        = string
  default     = "demo"
}

variable "lambda_package_path" {
  description = "Absolute path to the Lambda deployment zip file."
  type        = string
}

variable "lambda_package_hash" {
  description = "Base64-encoded SHA256 hash of the Lambda deployment zip file."
  type        = string
}

variable "permissions_boundary_arn" {
  description = "Optional permissions boundary ARN applied to IAM roles created by this stack."
  type        = string
  default     = null
  nullable    = true
}

variable "tags" {
  description = "Additional tags applied to demo resources."
  type        = map(string)
  default     = {}
}

variable "lambda_memory_size" {
  description = "Lambda memory size in MB."
  type        = number
  default     = 512
}

variable "lambda_timeout_seconds" {
  description = "Lambda timeout in seconds."
  type        = number
  default     = 30
}

variable "cache_duration_days" {
  description = "Number of days to cache successful geocode responses."
  type        = number
  default     = 30
}

variable "google_api_key_secret_json_key" {
  description = "JSON property name that stores the Google API key in Secrets Manager."
  type        = string
  default     = "apiKey"
}
