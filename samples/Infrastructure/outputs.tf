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
