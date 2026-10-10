variable "region" {
  type    = string
  default = "eu-west-2"
}

variable "name_prefix" {
  description = "Prefixes every resource's name: your own, so your stack doesn't collide with anyone else's."
  type        = string
}

variable "dev_users" {
  description = "The people to create in the dev user pool: each a user named by their employee id, with a password, and an employee record. Managers join the manager group."
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
