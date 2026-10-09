# The sample service: the core's resources and each entrypoint's, from the modules beside each project.
# Publish every project into artifacts_dir first; see samples/README.md.

locals {
  artifacts_dir = coalesce(var.artifacts_dir, "${path.root}/../artifacts")
}

module "core" {
  source = "../SampleService/infrastructure"

  name_prefix = var.name_prefix
}

module "queue_worker" {
  source = "../SampleService.QueueWorker/infrastructure"

  name_prefix           = var.name_prefix
  publish_dir           = "${local.artifacts_dir}/SampleService.QueueWorker"
  employees_table_name  = module.core.employees_table_name
  employees_policy_json = module.core.employees_policy_json
  topic_adapters = var.directory_events_topic_arn == null ? {} : {
    (var.directory_events_topic_arn) = "directory"
  }
}

module "api" {
  source = "../SampleService.Api/infrastructure"

  name_prefix           = var.name_prefix
  publish_dir           = "${local.artifacts_dir}/SampleService.Api"
  employees_table_name  = module.core.employees_table_name
  employees_policy_json = module.core.employees_policy_json
  work_queue_url        = module.queue_worker.work_queue_url
  work_queue_arn        = module.queue_worker.work_queue_arn
  jwt_issuer            = var.jwt_issuer
  jwt_audience          = var.jwt_audience
}

module "scheduled_worker" {
  source = "../SampleService.ScheduledWorker/infrastructure"

  name_prefix           = var.name_prefix
  publish_dir           = "${local.artifacts_dir}/SampleService.ScheduledWorker"
  employees_table_name  = module.core.employees_table_name
  employees_policy_json = module.core.employees_policy_json
  timezone              = var.timezone
  schedules = {
    review-reminders = {
      expression = "cron(0 8 ? * MON-FRI *)"
      link       = jsonencode({ "$feature" = "SendReviewReminders" })
    }
    archive-leavers = {
      expression = "cron(0 3 1 * ? *)"
      link       = jsonencode({ "$feature" = "ArchiveLeavers", AfterDays = 90 })
    }
  }
}
