variable "aws_account_id" {
  description = "AWS account id that owns the demo infrastructure."
  type        = string
}

variable "aws_region" {
  description = "AWS region for the bootstrap resources."
  type        = string
}

variable "environment" {
  description = "Environment name used in resource naming."
  type        = string
  default     = "demo"
}

variable "github_repository" {
  description = "GitHub repository allowed to assume the OIDC role, in owner/repo format."
  type        = string
}

variable "github_branch" {
  description = "GitHub branch allowed to assume the OIDC role."
  type        = string
  default     = "main"
}

variable "permissions_boundary_arn" {
  description = "Optional permissions boundary ARN applied to IAM roles created by this stack."
  type        = string
  default     = null
  nullable    = true
}

variable "tags" {
  description = "Additional tags applied to bootstrap resources."
  type        = map(string)
  default     = {}
}
