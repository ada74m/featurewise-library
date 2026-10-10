variable "name_prefix" {
  description = "Prefixes every resource's name, so several copies of the service can share an account."
  type        = string
}

variable "jwt_issuer" {
  description = "The issuer of UI users' tokens: the estate's sign-in, such as a Cognito user pool's URL."
  type        = string
}

variable "jwt_audience" {
  description = "The audiences a UI user's token may be for, such as the UI's Cognito app client id."
  type        = list(string)
}

variable "public_api_auth_domain" {
  description = "The prefix of the public API's token endpoint domain, which must be unique in the region across all AWS accounts. Defaults to <name_prefix>-public-api-<account id>."
  type        = string
  default     = null
}

variable "public_api_clients" {
  description = "The third-party clients of the public API, each mapped to the scopes it's granted."
  type        = map(list(string))
  default = {
    example-partner = ["hr/employees.read"]
  }
}

variable "directory_events_topic_arn" {
  description = "The directory service's SNS topic, whose AccountDeactivated events the queue worker handles."
  type        = string
  default     = null
}

variable "timezone" {
  description = "The time zone the job schedules are in."
  type        = string
  default     = "Europe/London"
}
