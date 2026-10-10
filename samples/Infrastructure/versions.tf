terraform {
  required_version = ">= 1.6"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 6.0"
    }
    archive = {
      source  = "hashicorp/archive"
      version = "~> 2.7"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.6"
    }
  }

  # State is local until a backend is configured here, such as
  # backend "s3" { bucket = "...", key = "sample-service.tfstate", region = "eu-west-2" }.
}

provider "aws" {
  region = var.region

  default_tags {
    tags = {
      Service = var.name_prefix
    }
  }
}
