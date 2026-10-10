# A dev user pool stands in for the estate's sign-in, which a UI shell shares across services. It has a
# manager group, and the people in dev_users: each a user named by their employee id, as the UI
# backend's rules expect, with a generated password and an employee record.

resource "aws_cognito_user_pool" "dev" {
  name = "${var.name_prefix}-dev-users"
}

# Signs in with a username and password, so a token can be had from the AWS CLI. A real UI signs in
# through the hosted sign-in page instead.
resource "aws_cognito_user_pool_client" "dev" {
  name                = "${var.name_prefix}-dev-ui"
  user_pool_id        = aws_cognito_user_pool.dev.id
  explicit_auth_flows = ["ALLOW_USER_PASSWORD_AUTH", "ALLOW_REFRESH_TOKEN_AUTH"]
}

resource "aws_cognito_user_group" "managers" {
  name         = "manager"
  user_pool_id = aws_cognito_user_pool.dev.id
}

resource "random_password" "dev_users" {
  for_each = var.dev_users

  length      = 20
  min_upper   = 1
  min_lower   = 1
  min_numeric = 1
  min_special = 1
}

resource "aws_cognito_user" "dev" {
  for_each = var.dev_users

  user_pool_id = aws_cognito_user_pool.dev.id
  username     = each.key
  password     = random_password.dev_users[each.key].result
}

resource "aws_cognito_user_in_group" "dev_managers" {
  for_each = { for id, user in var.dev_users : id => user if user.manager }

  user_pool_id = aws_cognito_user_pool.dev.id
  group_name   = aws_cognito_user_group.managers.name
  username     = aws_cognito_user.dev[each.key].username
}

# Each dev user's employee record, written once: the service changes it from then on, as when someone is
# fired, so later applies leave it alone.
resource "aws_dynamodb_table_item" "dev_employees" {
  for_each = var.dev_users

  table_name = module.hr.employees_table_name
  hash_key   = "Id"
  item = jsonencode(merge(
    {
      Id        = { S = each.key }
      Name      = { S = each.value.name }
      Status    = { S = "Active" }
      ReviewDue = { S = each.value.review_due }
    },
    each.value.manager_id == null ? {} : { ManagerId = { S = each.value.manager_id } }
  ))

  lifecycle {
    ignore_changes = [item]
  }
}
