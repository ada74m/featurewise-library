# The hr service: the core's resources and each entrypoint's, from the modules beside each project. This
# is the module a package carries, with each function's zip in its functions folder. An environment
# supplies the provider, the state, and the inputs that differ between environments.

data "aws_caller_identity" "current" {}

module "core" {
  source = "../SampleService/Infrastructure"

  name_prefix = var.name_prefix
}

module "queue_worker" {
  source = "../SampleService.QueueWorker/Infrastructure"

  name_prefix                = var.name_prefix
  employees_table_name       = module.core.employees_table_name
  employees_policy_json      = module.core.employees_policy_json
  events_topic_arn           = module.core.events_topic_arn
  events_publish_policy_json = module.core.events_publish_policy_json
  topic_adapters = var.directory_events_topic_arn == null ? {} : {
    (var.directory_events_topic_arn) = "directory"
  }
}

module "ui_backend" {
  source = "../SampleService.UiBackend/Infrastructure"

  name_prefix                = var.name_prefix
  employees_table_name       = module.core.employees_table_name
  employees_policy_json      = module.core.employees_policy_json
  events_topic_arn           = module.core.events_topic_arn
  events_publish_policy_json = module.core.events_publish_policy_json
  jwt_issuer                 = var.jwt_issuer
  jwt_audience               = var.jwt_audience
}

module "public_api" {
  source = "../SampleService.PublicApi/Infrastructure"

  name_prefix                = var.name_prefix
  employees_table_name       = module.core.employees_table_name
  employees_read_policy_json = module.core.employees_read_policy_json
  auth_domain                = coalesce(var.public_api_auth_domain, "${var.name_prefix}-public-api-${data.aws_caller_identity.current.account_id}")
  clients                    = var.public_api_clients
}

module "scheduled_worker" {
  source = "../SampleService.ScheduledWorker/Infrastructure"

  name_prefix           = var.name_prefix
  employees_table_name  = module.core.employees_table_name
  employees_policy_json = module.core.employees_policy_json
  work_queue_url        = module.queue_worker.work_queue_url
  work_queue_arn        = module.queue_worker.work_queue_arn
  timezone              = var.timezone
  schedules = {
    review-reminders = {
      expression = "cron(0 8 ? * MON-FRI *)"
      link       = jsonencode({ "$feature" = "hr/SendReviewReminders" })
    }
    archive-leavers = {
      expression = "cron(0 3 1 * ? *)"
      link       = jsonencode({ "$feature" = "hr/ArchiveLeavers", AfterDays = 90 })
    }
  }
}
