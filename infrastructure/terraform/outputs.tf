output "resource_group_name" {
  description = "The name of the resource group"
  value       = azurerm_resource_group.rg.name
}

output "app_service_name" {
  description = "The name of the App Service"
  value       = azurerm_windows_web_app.app_service.name
}

output "app_service_url" {
  description = "The URL of the App Service"
  value       = "https://${azurerm_windows_web_app.app_service.default_hostname}"
}

output "sql_server_name" {
  description = "The name of the SQL Server"
  value       = azurerm_mssql_server.sql_server.name
}

output "sql_server_fqdn" {
  description = "The fully qualified domain name of the SQL Server"
  value       = azurerm_mssql_server.sql_server.fully_qualified_domain_name
}

output "sql_database_name" {
  description = "The name of the SQL Database"
  value       = azurerm_mssql_database.database.name
}

output "key_vault_name" {
  description = "The name of the Key Vault"
  value       = azurerm_key_vault.key_vault.name
}

output "key_vault_uri" {
  description = "The URI of the Key Vault"
  value       = azurerm_key_vault.key_vault.vault_uri
}

output "service_bus_namespace" {
  description = "The name of the Service Bus Namespace"
  value       = azurerm_servicebus_namespace.service_bus.name
}

output "service_bus_queue_name" {
  description = "The name of the Service Bus Queue"
  value       = azurerm_servicebus_queue.pipeline_queue.name
}

output "storage_account_name" {
  description = "The name of the Storage Account"
  value       = azurerm_storage_account.storage.name
}

output "application_insights_name" {
  description = "The name of the Application Insights instance"
  value       = azurerm_application_insights.app_insights.name
}

output "application_insights_instrumentation_key" {
  description = "The instrumentation key of the Application Insights instance"
  value       = azurerm_application_insights.app_insights.instrumentation_key
  sensitive   = true
}

output "log_analytics_workspace_name" {
  description = "The name of the Log Analytics Workspace"
  value       = azurerm_log_analytics_workspace.log_analytics.name
}

output "virtual_network_name" {
  description = "The name of the Virtual Network"
  value       = azurerm_virtual_network.vnet.name
}

output "app_subnet_name" {
  description = "The name of the App Subnet"
  value       = azurerm_subnet.app_subnet.name
}

output "db_subnet_name" {
  description = "The name of the Database Subnet"
  value       = azurerm_subnet.db_subnet.name
}
