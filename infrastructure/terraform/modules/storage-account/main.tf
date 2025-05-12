variable "resource_group_name" {
  description = "The name of the resource group"
  type        = string
}

variable "location" {
  description = "The Azure region where resources will be created"
  type        = string
}

variable "storage_account_name" {
  description = "The name of the Storage Account"
  type        = string
}

variable "account_tier" {
  description = "The tier of the Storage Account"
  type        = string
  default     = "Standard"
}

variable "account_replication_type" {
  description = "The replication type of the Storage Account"
  type        = string
  default     = "LRS"
}

variable "account_kind" {
  description = "The kind of the Storage Account"
  type        = string
  default     = "StorageV2"
}

variable "access_tier" {
  description = "The access tier of the Storage Account"
  type        = string
  default     = "Hot"
}

variable "enable_https_traffic_only" {
  description = "Whether HTTPS traffic only is enabled"
  type        = bool
  default     = true
}

variable "min_tls_version" {
  description = "The minimum TLS version"
  type        = string
  default     = "TLS1_2"
}

variable "subnet_ids" {
  description = "A list of subnet IDs to allow access to the Storage Account"
  type        = list(string)
  default     = []
}

variable "containers" {
  description = "A list of containers to create in the Storage Account"
  type        = list(object({
    name                  = string
    container_access_type = string
  }))
  default     = []
}

variable "tags" {
  description = "A map of tags to apply to the resources"
  type        = map(string)
  default     = {}
}

resource "azurerm_storage_account" "storage" {
  name                     = var.storage_account_name
  resource_group_name      = var.resource_group_name
  location                 = var.location
  account_tier             = var.account_tier
  account_replication_type = var.account_replication_type
  account_kind             = var.account_kind
  access_tier              = var.access_tier
  enable_https_traffic_only = var.enable_https_traffic_only
  min_tls_version          = var.min_tls_version
  
  network_rules {
    default_action             = length(var.subnet_ids) > 0 ? "Deny" : "Allow"
    virtual_network_subnet_ids = var.subnet_ids
    bypass                     = ["AzureServices"]
  }
  
  tags = var.tags
}

resource "azurerm_storage_container" "containers" {
  for_each                = { for container in var.containers : container.name => container }
  
  name                    = each.value.name
  storage_account_name    = azurerm_storage_account.storage.name
  container_access_type   = each.value.container_access_type
}

output "storage_account_id" {
  description = "The ID of the Storage Account"
  value       = azurerm_storage_account.storage.id
}

output "storage_account_name" {
  description = "The name of the Storage Account"
  value       = azurerm_storage_account.storage.name
}

output "primary_blob_endpoint" {
  description = "The primary blob endpoint of the Storage Account"
  value       = azurerm_storage_account.storage.primary_blob_endpoint
}

output "primary_connection_string" {
  description = "The primary connection string of the Storage Account"
  value       = azurerm_storage_account.storage.primary_connection_string
  sensitive   = true
}

output "container_ids" {
  description = "The IDs of the Storage Containers"
  value       = { for name, container in azurerm_storage_container.containers : name => container.id }
}
