variable "name_prefix" {
  description = "Prefixes every resource's name."
  type        = string
}

variable "publish_dir" {
  description = "The directory dotnet publish wrote this project to."
  type        = string
}

variable "employees_table_name" {
  type = string
}

variable "employees_policy_json" {
  description = "Grants the function what the core's employee store does."
  type        = string
}

variable "work_queue_url" {
  description = "The queue features send links to, such as Fire's link to HandleEmployeeLeft."
  type        = string
}

variable "work_queue_arn" {
  type = string
}

variable "jwt_issuer" {
  description = "The issuer of callers' tokens, such as a Cognito user pool's URL. Without one, every caller is anonymous."
  type        = string
  default     = null
}

variable "jwt_audience" {
  description = "The audiences a caller's token may be for, such as a Cognito app client's id."
  type        = list(string)
  default     = []
}
