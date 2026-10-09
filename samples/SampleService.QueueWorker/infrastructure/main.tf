# The queue worker: the service's link queue, with its dead-letter queue, the function that invokes what
# arrives on it, and subscriptions to other services' topics.

resource "aws_sqs_queue" "dead_letters" {
  name                      = "${var.name_prefix}-links-dead-letters"
  message_retention_seconds = 1209600
}

resource "aws_sqs_queue" "links" {
  name = "${var.name_prefix}-links"
  # Six times the function's timeout, as Lambda recommends, so a message isn't redelivered while a
  # retried batch is still running.
  visibility_timeout_seconds = 180
  redrive_policy = jsonencode({
    deadLetterTargetArn = aws_sqs_queue.dead_letters.arn
    maxReceiveCount     = 5
  })
}

data "aws_iam_policy_document" "function" {
  source_policy_documents = [var.employees_policy_json]

  statement {
    actions   = ["sqs:ReceiveMessage", "sqs:DeleteMessage", "sqs:GetQueueAttributes"]
    resources = [aws_sqs_queue.links.arn]
  }

  # Handlers queue links too, such as HandleAccountDeactivated's link to HandleEmployeeLeft.
  statement {
    actions   = ["sqs:SendMessage"]
    resources = [aws_sqs_queue.links.arn]
  }
}

module "function" {
  source = "../../infrastructure/modules/lambda-function"

  name        = "${var.name_prefix}-queue-worker"
  publish_dir = var.publish_dir
  handler     = "SampleService.QueueWorker::SampleService.QueueWorker.Function::Handle"
  policy_json = data.aws_iam_policy_document.function.json
  environment = {
    EMPLOYEES_TABLE = var.employees_table_name
    LINK_QUEUE_URL  = aws_sqs_queue.links.url
    TOPIC_ADAPTERS  = join(";", [for topic, adapter in var.topic_adapters : "${topic}=${adapter}"])
  }
}

# The function reports which messages failed, so SQS retries only those.
resource "aws_lambda_event_source_mapping" "links" {
  event_source_arn        = aws_sqs_queue.links.arn
  function_name           = module.function.function_arn
  batch_size              = 10
  function_response_types = ["ReportBatchItemFailures"]
}

# Without raw message delivery, so each message keeps its SNS envelope, which names its topic.
resource "aws_sns_topic_subscription" "topics" {
  for_each = var.topic_adapters

  topic_arn = each.key
  protocol  = "sqs"
  endpoint  = aws_sqs_queue.links.arn
}

data "aws_iam_policy_document" "links_queue" {
  count = length(var.topic_adapters) == 0 ? 0 : 1

  statement {
    actions   = ["sqs:SendMessage"]
    resources = [aws_sqs_queue.links.arn]
    principals {
      type        = "Service"
      identifiers = ["sns.amazonaws.com"]
    }
    condition {
      test     = "ArnEquals"
      variable = "aws:SourceArn"
      values   = keys(var.topic_adapters)
    }
  }
}

resource "aws_sqs_queue_policy" "links" {
  count = length(var.topic_adapters) == 0 ? 0 : 1

  queue_url = aws_sqs_queue.links.id
  policy    = data.aws_iam_policy_document.links_queue[0].json
}
