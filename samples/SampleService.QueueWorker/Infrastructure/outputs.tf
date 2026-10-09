output "work_queue_url" {
  value = aws_sqs_queue.work.url
}

output "work_queue_arn" {
  value = aws_sqs_queue.work.arn
}

output "dead_letter_queue_url" {
  value = aws_sqs_queue.dead_letters.url
}
