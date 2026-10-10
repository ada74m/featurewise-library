# The scheduled worker: one function, invoked by an EventBridge schedule per job.

# SendReviewReminders queues a reminder for each manager.
data "aws_iam_policy_document" "function" {
  source_policy_documents = [var.employees_policy_json]

  statement {
    actions   = ["sqs:SendMessage"]
    resources = [var.work_queue_arn]
  }
}

module "function" {
  source = "../../Infrastructure/modules/lambda-function"

  name        = "${var.name_prefix}-scheduled-worker"
  package_zip = "${path.module}/../../functions/SampleService.ScheduledWorker.zip"
  handler     = "SampleService.ScheduledWorker::SampleService.ScheduledWorker.Function::Handle"
  policy_json = data.aws_iam_policy_document.function.json
  timeout     = 300
  environment = {
    EMPLOYEES_TABLE = var.employees_table_name
    WORK_QUEUE_URL  = var.work_queue_url
  }
}

data "aws_iam_policy_document" "scheduler_assume_role" {
  statement {
    actions = ["sts:AssumeRole"]
    principals {
      type        = "Service"
      identifiers = ["scheduler.amazonaws.com"]
    }
  }
}

resource "aws_iam_role" "scheduler" {
  name               = "${var.name_prefix}-scheduler"
  assume_role_policy = data.aws_iam_policy_document.scheduler_assume_role.json
}

data "aws_iam_policy_document" "scheduler" {
  statement {
    actions   = ["lambda:InvokeFunction"]
    resources = [module.function.function_arn]
  }
}

resource "aws_iam_role_policy" "scheduler" {
  name   = "invoke-scheduled-worker"
  role   = aws_iam_role.scheduler.id
  policy = data.aws_iam_policy_document.scheduler.json
}

# Each schedule sends the function its job's link, with the schedule's ARN, whose name becomes the
# job's caller, and the execution's id, which stays the same across retries and so serves as the
# idempotency key.
resource "aws_scheduler_schedule" "jobs" {
  for_each = var.schedules

  name                         = "${var.name_prefix}-${each.key}"
  schedule_expression          = each.value.expression
  schedule_expression_timezone = var.timezone

  flexible_time_window {
    mode = "OFF"
  }

  target {
    arn      = module.function.function_arn
    role_arn = aws_iam_role.scheduler.arn
    input = jsonencode({
      Schedule  = "<aws.scheduler.schedule-arn>"
      Execution = "<aws.scheduler.execution-id>"
      Link      = jsondecode(each.value.link)
    })

    retry_policy {
      maximum_retry_attempts = 3
    }
  }
}
