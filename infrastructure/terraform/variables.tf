variable "environment" {
  description = "Environment (dev, test, prod)"
  type        = string
  default     = "dev"
}

variable "location" {
  description = "Azure region"
  type        = string
  default     = "East US"
}

variable "resource_group_name" {
  description = "Resource group name"
  type        = string
  default     = "rg-dataprocessingservice"
}

variable "app_name" {
  description = "Application name"
  type        = string
  default     = "dataprocessingservice"
}

variable "sql_admin_username" {
  description = "SQL Server admin username"
  type        = string
  default     = "sqladmin"
}

variable "sql_admin_password" {
  description = "SQL Server admin password"
  type        = string
  sensitive   = true
}

variable "app_service_sku" {
  description = "App Service Plan SKU"
  type        = map(string)
  default = {
    dev  = "B1"
    test = "B2"
    prod = "P1v2"
  }
}

variable "sql_database_sku" {
  description = "SQL Database SKU"
  type        = map(string)
  default = {
    dev  = "Basic"
    test = "S0"
    prod = "S1"
  }
}

variable "service_bus_sku" {
  description = "Service Bus SKU"
  type        = map(string)
  default = {
    dev  = "Basic"
    test = "Standard"
    prod = "Standard"
  }
}

variable "storage_account_replication" {
  description = "Storage Account replication type"
  type        = map(string)
  default = {
    dev  = "LRS"
    test = "ZRS"
    prod = "GRS"
  }
}

variable "log_retention_days" {
  description = "Log retention in days"
  type        = map(number)
  default = {
    dev  = 30
    test = 60
    prod = 90
  }
}

variable "allowed_ip_addresses" {
  description = "List of IP addresses allowed to access resources"
  type        = list(string)
  default     = []
}
