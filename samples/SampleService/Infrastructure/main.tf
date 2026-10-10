# What the core's features use whichever entrypoint invokes them: the employees table.

resource "aws_dynamodb_table" "employees" {
  name         = "${var.name_prefix}-employees"
  billing_mode = "PAY_PER_REQUEST"
  hash_key     = "Id"

  attribute {
    name = "Id"
    type = "S"
  }

  point_in_time_recovery {
    enabled = true
  }
}

# The service's topic, where it publishes its events, the records in Events/. Other services subscribe.
resource "aws_sns_topic" "events" {
  name = "${var.name_prefix}-events"
}

data "aws_iam_policy_document" "events_publish" {
  statement {
    actions   = ["sns:Publish"]
    resources = [aws_sns_topic.events.arn]
  }
}

# What DynamoEmployeeStore does, for each function's policy.
data "aws_iam_policy_document" "employees" {
  statement {
    actions   = ["dynamodb:GetItem", "dynamodb:PutItem", "dynamodb:DeleteItem", "dynamodb:Scan"]
    resources = [aws_dynamodb_table.employees.arn]
  }
}

# Only what DynamoEmployeeStore does to read, for functions that never write.
data "aws_iam_policy_document" "employees_read" {
  statement {
    actions   = ["dynamodb:GetItem", "dynamodb:Scan"]
    resources = [aws_dynamodb_table.employees.arn]
  }
}
