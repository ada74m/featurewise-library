output "ui_backend_url" {
  value = module.ui_backend.url
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
