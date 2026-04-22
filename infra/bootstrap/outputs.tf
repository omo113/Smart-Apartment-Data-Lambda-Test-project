output "github_actions_role_arn" {
  description = "IAM role ARN assumed by GitHub Actions through OIDC."
  value       = aws_iam_role.github_actions_terraform.arn
}

output "tf_state_bucket" {
  description = "S3 bucket that stores Terraform state for the demo environment."
  value       = aws_s3_bucket.terraform_state.bucket
}

output "tf_lock_table" {
  description = "DynamoDB table used for Terraform state locking."
  value       = aws_dynamodb_table.terraform_lock.name
}
