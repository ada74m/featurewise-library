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

variable "schedules" {
  description = "The jobs to run, by schedule name: each schedule's expression, and its job's link as JSON, such as jsonencode({ \"$feature\" = \"ArchiveLeavers\", AfterDays = 90 })."
  type = map(object({
    expression = string
    link       = string
  }))
}

variable "timezone" {
  description = "The time zone schedule expressions are in."
  type        = string
  default     = "UTC"
}
