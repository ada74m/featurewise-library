# The API: an HTTP API whose two routes, GET and POST /{feature}, reach one function.

data "aws_iam_policy_document" "function" {
  source_policy_documents = [var.employees_policy_json]

  statement {
    actions   = ["sqs:SendMessage"]
    resources = [var.link_queue_arn]
  }
}

module "function" {
  source = "../../infrastructure/modules/lambda-function"

  name        = "${var.name_prefix}-api"
  publish_dir = var.publish_dir
  handler     = "SampleService.Api::SampleService.Api.Function::Handle"
  policy_json = data.aws_iam_policy_document.function.json
  environment = {
    EMPLOYEES_TABLE = var.employees_table_name
    LINK_QUEUE_URL  = var.link_queue_url
  }
}

resource "aws_apigatewayv2_api" "api" {
  name          = "${var.name_prefix}-api"
  protocol_type = "HTTP"
}

resource "aws_apigatewayv2_integration" "function" {
  api_id                 = aws_apigatewayv2_api.api.id
  integration_type       = "AWS_PROXY"
  integration_uri        = module.function.invoke_arn
  payload_format_version = "2.0"
}

# Verifies callers' tokens, so the function trusts the claims it's given. API Gateway refuses a request
# without a valid token before it reaches the function.
resource "aws_apigatewayv2_authorizer" "jwt" {
  count = var.jwt_issuer == null ? 0 : 1

  api_id           = aws_apigatewayv2_api.api.id
  name             = "jwt"
  authorizer_type  = "JWT"
  identity_sources = ["$request.header.Authorization"]

  jwt_configuration {
    issuer   = var.jwt_issuer
    audience = var.jwt_audience
  }
}

# Queries by GET, commands by POST. The function knows which features are which.
resource "aws_apigatewayv2_route" "features" {
  for_each = toset(["GET /{feature}", "POST /{feature}"])

  api_id             = aws_apigatewayv2_api.api.id
  route_key          = each.key
  target             = "integrations/${aws_apigatewayv2_integration.function.id}"
  authorization_type = var.jwt_issuer == null ? "NONE" : "JWT"
  authorizer_id      = var.jwt_issuer == null ? null : aws_apigatewayv2_authorizer.jwt[0].id
}

resource "aws_apigatewayv2_stage" "default" {
  api_id      = aws_apigatewayv2_api.api.id
  name        = "$default"
  auto_deploy = true
}

resource "aws_lambda_permission" "api" {
  action        = "lambda:InvokeFunction"
  function_name = module.function.function_name
  principal     = "apigateway.amazonaws.com"
  source_arn    = "${aws_apigatewayv2_api.api.execution_arn}/*/*"
}
