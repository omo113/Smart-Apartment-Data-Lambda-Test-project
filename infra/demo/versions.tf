terraform {
  required_version = ">= 1.5.0"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 6.0"
    }
  }

  backend "s3" {
    bucket         = "smart-apartment-tfstate-590183851689-eu-north-1"
    key            = "demo/terraform.tfstate"
    region         = "eu-north-1"
    dynamodb_table = "smart-apartment-tf-locks-demo"
    encrypt        = true
  }
}
