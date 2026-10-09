variable "region" {
  type    = string
  default = "eu-west-2"
}

variable "name_prefix" {
  description = "Prefixes every resource's name."
  type        = string
  default     = "hr"
}

variable "artifacts_dir" {
  description = "Where each project was published, one directory per project. Defaults to samples/artifacts."
  type        = string
  default     = null
}

variable "jwt_issuer" {
  description = "The issuer of UI backend callers' tokens, such as a Cognito user pool's URL. Without one, every UI backend caller is anonymous."
  type        = string
  default     = null
}

variable "jwt_audience" {
  type    = list(string)
  default = []
}

variable "public_api_auth_domain" {
  description = "The prefix of the public API's token endpoint domain, which must be unique in the region. Defaults to <name_prefix>-public-api."
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
