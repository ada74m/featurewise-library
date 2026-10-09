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

# What DynamoEmployeeStore does, for each function's policy.
data "aws_iam_policy_document" "employees" {
  statement {
    actions   = ["dynamodb:GetItem", "dynamodb:PutItem", "dynamodb:DeleteItem", "dynamodb:Scan"]
    resources = [aws_dynamodb_table.employees.arn]
  }
}
