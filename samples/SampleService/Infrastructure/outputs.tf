output "employees_table_name" {
  value = aws_dynamodb_table.employees.name
}

output "events_topic_arn" {
  value = aws_sns_topic.events.arn
}

output "events_publish_policy_json" {
  description = "Grants publishing to the service's topic, for functions whose features publish events."
  value       = data.aws_iam_policy_document.events_publish.json
}

output "employees_policy_json" {
  description = "Grants what DynamoEmployeeStore does to the employees table."
  value       = data.aws_iam_policy_document.employees.json
}

output "employees_read_policy_json" {
  description = "Grants what DynamoEmployeeStore does to read the employees table."
  value       = data.aws_iam_policy_document.employees_read.json
}
