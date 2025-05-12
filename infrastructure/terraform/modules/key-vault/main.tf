variable "resource_group_name" {
  description = "The name of the resource group"
  type        = string
}

variable "location" {
  description = "The Azure region where resources will be created"
  type        = string
}

variable "key_vault_name" {
  description = "The name of the Key Vault"
  type        = string
}

variable "sku_name" {
  description = "The SKU name for the Key Vault"
  type        = string
  default     = "standard"
}

variable "subnet_ids" {
  description = "A list of subnet IDs to allow access to the Key Vault"
  type        = list(string)
  default     = []
}

variable "access_policies" {
  description = "A list of access policies to apply to the Key Vault"
  type        = list(object({
    object_id               = string
    tenant_id               = string
    key_permissions         = list(string)
    secret_permissions      = list(string)
    certificate_permissions = list(string)
    storage_permissions     = list(string)
  }))
  default     = []
}

variable "secrets" {
  description = "A map of secrets to add to the Key Vault"
  type        = map(string)
  default     = {}
  sensitive   = true
}

variable "tags" {
  description = "A map of tags to apply to the resources"
  type        = map(string)
  default     = {}
}

data "azurerm_client_config" "current" {}

resource "azurerm_key_vault" "key_vault" {
  name                        = var.key_vault_name
  location                    = var.location
  resource_group_name         = var.resource_group_name
  enabled_for_disk_encryption = true
  tenant_id                   = data.azurerm_client_config.current.tenant_id
  soft_delete_retention_days  = 7
  purge_protection_enabled    = false
  
  sku_name = var.sku_name
  
  network_acls {
    default_action             = length(var.subnet_ids) > 0 ? "Deny" : "Allow"
    bypass                     = "AzureServices"
    virtual_network_subnet_ids = var.subnet_ids
    ip_rules                   = []
  }
  
  tags = var.tags
}

resource "azurerm_key_vault_access_policy" "terraform" {
  key_vault_id = azurerm_key_vault.key_vault.id
  tenant_id    = data.azurerm_client_config.current.tenant_id
  object_id    = data.azurerm_client_config.current.object_id
  
  key_permissions = [
    "Get", "List", "Create", "Delete", "Update",
  ]
  
  secret_permissions = [
    "Get", "List", "Set", "Delete",
  ]
  
  certificate_permissions = [
    "Get", "List", "Create", "Delete",
  ]
}

resource "azurerm_key_vault_access_policy" "policies" {
  for_each     = { for policy in var.access_policies : policy.object_id => policy }
  
  key_vault_id = azurerm_key_vault.key_vault.id
  tenant_id    = each.value.tenant_id
  object_id    = each.value.object_id
  
  key_permissions         = each.value.key_permissions
  secret_permissions      = each.value.secret_permissions
  certificate_permissions = each.value.certificate_permissions
  storage_permissions     = each.value.storage_permissions
}

resource "azurerm_key_vault_secret" "secrets" {
  for_each     = var.secrets
  
  name         = each.key
  value        = each.value
  key_vault_id = azurerm_key_vault.key_vault.id
  
  depends_on = [
    azurerm_key_vault_access_policy.terraform
  ]
}

output "key_vault_id" {
  description = "The ID of the Key Vault"
  value       = azurerm_key_vault.key_vault.id
}

output "key_vault_uri" {
  description = "The URI of the Key Vault"
  value       = azurerm_key_vault.key_vault.vault_uri
}

output "key_vault_name" {
  description = "The name of the Key Vault"
  value       = azurerm_key_vault.key_vault.name
}
