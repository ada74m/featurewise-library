output "ui_backend_url" {
  value = module.hr.ui_backend_url
}

output "public_api_url" {
  value = module.hr.public_api_url
}

output "public_api_token_url" {
  value = module.hr.public_api_token_url
}

output "public_api_clients" {
  sensitive = true
  value     = module.hr.public_api_clients
}

output "events_topic_arn" {
  value = module.hr.events_topic_arn
}

output "work_queue_url" {
  value = module.hr.work_queue_url
}

output "dead_letter_queue_url" {
  value = module.hr.dead_letter_queue_url
}

output "dev_ui_client_id" {
  description = "The dev user pool's app client, for signing in from the AWS CLI."
  value       = aws_cognito_user_pool_client.dev.id
}

output "dev_user_passwords" {
  description = "Each dev user's password, by username."
  sensitive   = true
  value       = { for id, password in random_password.dev_users : id => password.result }
}
