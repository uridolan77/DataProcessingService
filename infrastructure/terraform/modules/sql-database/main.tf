variable "resource_group_name" {
  description = "The name of the resource group"
  type        = string
}

variable "location" {
  description = "The Azure region where resources will be created"
  type        = string
}

variable "server_name" {
  description = "The name of the SQL Server"
  type        = string
}

variable "database_name" {
  description = "The name of the SQL Database"
  type        = string
}

variable "admin_username" {
  description = "The administrator username for the SQL Server"
  type        = string
}

variable "admin_password" {
  description = "The administrator password for the SQL Server"
  type        = string
  sensitive   = true
}

variable "sku_name" {
  description = "The SKU name for the SQL Database"
  type        = string
  default     = "Basic"
}

variable "max_size_gb" {
  description = "The maximum size of the SQL Database in gigabytes"
  type        = number
  default     = 4
}

variable "zone_redundant" {
  description = "Whether the SQL Database should be zone redundant"
  type        = bool
  default     = false
}

variable "subnet_id" {
  description = "The ID of the subnet to create a virtual network rule for"
  type        = string
  default     = null
}

variable "allowed_ip_addresses" {
  description = "A list of IP addresses allowed to access the SQL Server"
  type        = list(object({
    name             = string
    start_ip_address = string
    end_ip_address   = string
  }))
  default     = []
}

variable "tags" {
  description = "A map of tags to apply to the resources"
  type        = map(string)
  default     = {}
}

resource "azurerm_mssql_server" "server" {
  name                         = var.server_name
  resource_group_name          = var.resource_group_name
  location                     = var.location
  version                      = "12.0"
  administrator_login          = var.admin_username
  administrator_login_password = var.admin_password
  minimum_tls_version          = "1.2"
  
  azuread_administrator {
    login_username = "AzureAD Admin"
    object_id      = data.azurerm_client_config.current.object_id
  }
  
  tags = var.tags
}

resource "azurerm_mssql_database" "database" {
  name                = var.database_name
  server_id           = azurerm_mssql_server.server.id
  collation           = "SQL_Latin1_General_CP1_CI_AS"
  max_size_gb         = var.max_size_gb
  read_scale          = false
  sku_name            = var.sku_name
  zone_redundant      = var.zone_redundant
  
  tags = var.tags
}

resource "azurerm_mssql_firewall_rule" "allow_azure_services" {
  name             = "AllowAzureServices"
  server_id        = azurerm_mssql_server.server.id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "0.0.0.0"
}

resource "azurerm_mssql_firewall_rule" "allowed_ips" {
  for_each          = { for rule in var.allowed_ip_addresses : rule.name => rule }
  
  name              = each.value.name
  server_id         = azurerm_mssql_server.server.id
  start_ip_address  = each.value.start_ip_address
  end_ip_address    = each.value.end_ip_address
}

resource "azurerm_mssql_virtual_network_rule" "vnet_rule" {
  count     = var.subnet_id != null ? 1 : 0
  name      = "sql-vnet-rule"
  server_id = azurerm_mssql_server.server.id
  subnet_id = var.subnet_id
}

data "azurerm_client_config" "current" {}

output "server_name" {
  description = "The name of the SQL Server"
  value       = azurerm_mssql_server.server.name
}

output "server_fqdn" {
  description = "The fully qualified domain name of the SQL Server"
  value       = azurerm_mssql_server.server.fully_qualified_domain_name
}

output "database_name" {
  description = "The name of the SQL Database"
  value       = azurerm_mssql_database.database.name
}

output "connection_string" {
  description = "The connection string for the SQL Database"
  value       = "Server=tcp:${azurerm_mssql_server.server.fully_qualified_domain_name},1433;Initial Catalog=${azurerm_mssql_database.database.name};Persist Security Info=False;User ID=${var.admin_username};Password=${var.admin_password};MultipleActiveResultSets=True;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
  sensitive   = true
}
