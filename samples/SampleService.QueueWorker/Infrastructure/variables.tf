variable "name_prefix" {
  description = "Prefixes every resource's name."
  type        = string
}

variable "employees_table_name" {
  type = string
}

variable "employees_policy_json" {
  description = "Grants the function what the core's employee store does."
  type        = string
}

variable "events_topic_arn" {
  description = "The service's topic, where its features publish its events."
  type        = string
}

variable "events_publish_policy_json" {
  description = "Grants publishing to the service's topic."
  type        = string
}

variable "topic_adapters" {
  description = "Other services' SNS topics to subscribe to, each mapped to the name of the adapter that turns its events into links."
  type        = map(string)
  default     = {}
}
