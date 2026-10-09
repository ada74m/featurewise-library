output "employees_table_name" {
  value = aws_dynamodb_table.employees.name
}

output "employees_policy_json" {
  description = "Grants what DynamoEmployeeStore does to the employees table."
  value       = data.aws_iam_policy_document.employees.json
}

output "employees_read_policy_json" {
  description = "Grants what DynamoEmployeeStore does to read the employees table."
  value       = data.aws_iam_policy_document.employees_read.json
}
