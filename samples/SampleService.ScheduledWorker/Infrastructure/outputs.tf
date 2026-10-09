output "schedule_names" {
  value = [for schedule in aws_scheduler_schedule.jobs : schedule.name]
}
