output "api_url" {
  value = module.api.api_url
}

output "link_queue_url" {
  value = module.queue_worker.link_queue_url
}

output "dead_letter_queue_url" {
  value = module.queue_worker.dead_letter_queue_url
}

output "employees_table_name" {
  value = module.core.employees_table_name
}
