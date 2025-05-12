variable "resource_group_name" {
  description = "The name of the resource group"
  type        = string
}

variable "location" {
  description = "The Azure region where resources will be created"
  type        = string
}

variable "app_service_name" {
  description = "The name of the App Service"
  type        = string
}

variable "app_service_plan_name" {
  description = "The name of the App Service Plan"
  type        = string
}

variable "sku_name" {
  description = "The SKU name for the App Service Plan"
  type        = string
  default     = "B1"
}

variable "always_on" {
  description = "Whether the App Service should be always on"
  type        = bool
  default     = false
}

variable "app_settings" {
  description = "A map of app settings to apply to the App Service"
  type        = map(string)
  default     = {}
}

variable "connection_strings" {
  description = "A list of connection strings to apply to the App Service"
  type        = list(object({
    name  = string
    type  = string
    value = string
  }))
  default     = []
}

variable "subnet_id" {
  description = "The ID of the subnet to integrate with the App Service"
  type        = string
  default     = null
}

variable "tags" {
  description = "A map of tags to apply to the resources"
  type        = map(string)
  default     = {}
}

resource "azurerm_service_plan" "app_plan" {
  name                = var.app_service_plan_name
  location            = var.location
  resource_group_name = var.resource_group_name
  os_type             = "Windows"
  sku_name            = var.sku_name
  
  tags = var.tags
}

resource "azurerm_windows_web_app" "app" {
  name                = var.app_service_name
  location            = var.location
  resource_group_name = var.resource_group_name
  service_plan_id     = azurerm_service_plan.app_plan.id
  
  https_only = true
  
  site_config {
    always_on        = var.always_on
    ftps_state       = "Disabled"
    min_tls_version  = "1.2"
    
    application_stack {
      dotnet_version = "v9.0"
    }
  }
  
  app_settings = var.app_settings
  
  dynamic "connection_string" {
    for_each = var.connection_strings
    content {
      name  = connection_string.value.name
      type  = connection_string.value.type
      value = connection_string.value.value
    }
  }
  
  identity {
    type = "SystemAssigned"
  }
  
  tags = var.tags
}

resource "azurerm_app_service_virtual_network_swift_connection" "vnet_integration" {
  count          = var.subnet_id != null ? 1 : 0
  app_service_id = azurerm_windows_web_app.app.id
  subnet_id      = var.subnet_id
}

output "app_service_name" {
  description = "The name of the App Service"
  value       = azurerm_windows_web_app.app.name
}

output "app_service_url" {
  description = "The URL of the App Service"
  value       = "https://${azurerm_windows_web_app.app.default_hostname}"
}

output "app_service_id" {
  description = "The ID of the App Service"
  value       = azurerm_windows_web_app.app.id
}

output "principal_id" {
  description = "The Principal ID of the App Service's Managed Identity"
  value       = azurerm_windows_web_app.app.identity[0].principal_id
}

output "tenant_id" {
  description = "The Tenant ID of the App Service's Managed Identity"
  value       = azurerm_windows_web_app.app.identity[0].tenant_id
}
