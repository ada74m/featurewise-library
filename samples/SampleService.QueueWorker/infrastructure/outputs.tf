output "link_queue_url" {
  value = aws_sqs_queue.links.url
}

output "link_queue_arn" {
  value = aws_sqs_queue.links.arn
}

output "dead_letter_queue_url" {
  value = aws_sqs_queue.dead_letters.url
}
