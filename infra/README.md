# Terraform Infrastructure

This repo now contains two Terraform stacks:

- `infra/bootstrap`
  Creates the S3 backend bucket, DynamoDB lock table, GitHub OIDC provider, and the GitHub Actions IAM role.
- `infra/demo`
  Creates the Lambda, API Gateway HTTP API, DynamoDB cache table, CloudWatch log group, and the Secrets Manager secret shell for the Google API key.

## One-Time Bootstrap

Run this stack locally with AWS credentials that are allowed to create IAM, S3, and DynamoDB resources in account `590183851689`.

```bash
cp infra/bootstrap/terraform.tfvars.example infra/bootstrap/terraform.tfvars
terraform -chdir=infra/bootstrap init
terraform -chdir=infra/bootstrap apply
```

If the account already has the GitHub OIDC provider, import it before `apply`:

```bash
terraform -chdir=infra/bootstrap import \
  aws_iam_openid_connect_provider.github_actions \
  arn:aws:iam::590183851689:oidc-provider/token.actions.githubusercontent.com
```

Bootstrap outputs:

- `github_actions_role_arn`
- `tf_state_bucket`
- `tf_lock_table`

## Demo Deployment Flow

After bootstrap succeeds, pushes to `main` trigger `.github/workflows/terraform-demo.yml`.

The workflow will:

1. Restore, test, and publish the Lambda.
2. Zip the published output and calculate a base64 SHA256 hash.
3. Assume `arn:aws:iam::590183851689:role/github-actions-terraform-smart-apartment-demo` through GitHub OIDC.
4. Run `terraform fmt -check`, `terraform validate`, `terraform plan`, and `terraform apply`.

## Google API Secret

Terraform creates the secret metadata only. Set the value manually after the first demo apply:

```bash
aws secretsmanager put-secret-value \
  --region eu-north-1 \
  --secret-id smart-apartment/google-geocoding/demo \
  --secret-string '{"apiKey":"YOUR_GOOGLE_GEOCODING_API_KEY"}'
```

Once the secret value exists, push to `main` again or rerun the workflow to deploy the Lambda with a working secret reference.

## Local Demo Stack Commands

If you want to run the demo stack locally instead of CI:

```bash
cp infra/demo/terraform.tfvars.example infra/demo/terraform.tfvars
terraform -chdir=infra/demo init
terraform -chdir=infra/demo plan \
  -var="lambda_package_path=/absolute/path/to/SmartApartmentLambda.Service.zip" \
  -var="lambda_package_hash=$(openssl dgst -sha256 -binary /absolute/path/to/SmartApartmentLambda.Service.zip | openssl base64 -A)"
```

The HTTP API integration is configured with payload format `1.0` because the Lambda handler uses `APIGatewayProxyRequest`.
