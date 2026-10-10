# A developer's own environment, deployed from a laptop by samples/deploy.sh. It uses the package
# package.sh builds, unpacked in samples/out/hr, the same as a real environment uses a published one.
# State is local. Real environments live in the environments repo, with state in S3.

module "hr" {
  source = "../out/hr/Infrastructure"

  name_prefix  = var.name_prefix
  jwt_issuer   = "https://${aws_cognito_user_pool.dev.endpoint}"
  jwt_audience = [aws_cognito_user_pool_client.dev.id]
}
