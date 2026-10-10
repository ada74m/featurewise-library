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
  description = "The issuer of UI users' tokens: the estate's sign-in, such as a Cognito user pool's URL. Without one, a dev user pool is created in its place, with the users in dev_users."
  type        = string
  default     = null
}

variable "jwt_audience" {
  description = "The audiences a UI user's token may be for, such as the UI's Cognito app client id. Ignored without jwt_issuer."
  type        = list(string)
  default     = []
}

variable "dev_users" {
  description = "With the dev user pool, the people to create: each a user named by their employee id, with a password, and an employee record. Managers join the manager group."
  type = map(object({
    name       = string
    manager    = optional(bool, false)
    manager_id = optional(string)
    review_due = optional(string, "2027-01-15")
  }))
  default = {
    m1 = { name = "Mia", manager = true }
    e1 = { name = "Ed", manager_id = "m1" }
  }
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
