output "ui_backend_url" {
  value = module.ui_backend.url
}

output "public_api_url" {
  value = module.public_api.url
}

output "public_api_token_url" {
  value = module.public_api.token_url
}

output "public_api_clients" {
  sensitive = true
  value     = module.public_api.clients
}

output "work_queue_url" {
  value = module.queue_worker.work_queue_url
}

output "dead_letter_queue_url" {
  value = module.queue_worker.dead_letter_queue_url
}

output "employees_table_name" {
  value = module.core.employees_table_name
}

output "events_topic_arn" {
  description = "The service's topic, which other services subscribe to for its events."
  value       = module.core.events_topic_arn
}

output "dev_ui_client_id" {
  description = "The dev user pool's app client, for signing in from the AWS CLI. Null with jwt_issuer."
  value       = local.dev_pool ? aws_cognito_user_pool_client.dev[0].id : null
}

output "dev_user_passwords" {
  description = "Each dev user's password, by username."
  sensitive   = true
  value       = { for id, password in random_password.dev_users : id => password.result }
}
