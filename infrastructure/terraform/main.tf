terraform {
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.0"
    }
  }
  
  backend "azurerm" {
    # These values will be filled in during CI/CD pipeline execution
    # resource_group_name  = "tfstate"
    # storage_account_name = "tfstate"
    # container_name       = "tfstate"
    # key                  = "dataprocessingservice.tfstate"
  }
}

provider "azurerm" {
  features {
    key_vault {
      purge_soft_delete_on_destroy = true
    }
  }
}

# Define variables
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

# Local variables
locals {
  tags = {
    Environment = var.environment
    Application = var.app_name
    ManagedBy   = "Terraform"
  }
  
  resource_name_prefix = "${var.app_name}-${var.environment}"
}

# Resource Group
resource "azurerm_resource_group" "rg" {
  name     = "${var.resource_group_name}-${var.environment}"
  location = var.location
  tags     = local.tags
}

# Virtual Network
resource "azurerm_virtual_network" "vnet" {
  name                = "${local.resource_name_prefix}-vnet"
  address_space       = ["10.0.0.0/16"]
  location            = azurerm_resource_group.rg.location
  resource_group_name = azurerm_resource_group.rg.name
  tags                = local.tags
}

# Subnets
resource "azurerm_subnet" "app_subnet" {
  name                 = "${local.resource_name_prefix}-app-subnet"
  resource_group_name  = azurerm_resource_group.rg.name
  virtual_network_name = azurerm_virtual_network.vnet.name
  address_prefixes     = ["10.0.1.0/24"]
  service_endpoints    = ["Microsoft.Sql", "Microsoft.Storage", "Microsoft.ServiceBus", "Microsoft.KeyVault"]
  
  delegation {
    name = "app-service-delegation"
    
    service_delegation {
      name    = "Microsoft.Web/serverFarms"
      actions = ["Microsoft.Network/virtualNetworks/subnets/action"]
    }
  }
}

resource "azurerm_subnet" "db_subnet" {
  name                 = "${local.resource_name_prefix}-db-subnet"
  resource_group_name  = azurerm_resource_group.rg.name
  virtual_network_name = azurerm_virtual_network.vnet.name
  address_prefixes     = ["10.0.2.0/24"]
  service_endpoints    = ["Microsoft.Sql"]
}

# Application Insights
resource "azurerm_application_insights" "app_insights" {
  name                = "${local.resource_name_prefix}-appinsights"
  location            = azurerm_resource_group.rg.location
  resource_group_name = azurerm_resource_group.rg.name
  application_type    = "web"
  tags                = local.tags
}

# Log Analytics Workspace
resource "azurerm_log_analytics_workspace" "log_analytics" {
  name                = "${local.resource_name_prefix}-logs"
  location            = azurerm_resource_group.rg.location
  resource_group_name = azurerm_resource_group.rg.name
  sku                 = "PerGB2018"
  retention_in_days   = 30
  tags                = local.tags
}

# Key Vault
resource "azurerm_key_vault" "key_vault" {
  name                        = "${local.resource_name_prefix}-kv"
  location                    = azurerm_resource_group.rg.location
  resource_group_name         = azurerm_resource_group.rg.name
  enabled_for_disk_encryption = true
  tenant_id                   = data.azurerm_client_config.current.tenant_id
  soft_delete_retention_days  = 7
  purge_protection_enabled    = false
  
  sku_name = "standard"
  
  network_acls {
    default_action             = "Deny"
    bypass                     = "AzureServices"
    virtual_network_subnet_ids = [azurerm_subnet.app_subnet.id]
    ip_rules                   = []
  }
  
  tags = local.tags
}

# Get current client configuration
data "azurerm_client_config" "current" {}

# Key Vault Access Policy for Terraform
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

# Storage Account
resource "azurerm_storage_account" "storage" {
  name                     = "${replace(local.resource_name_prefix, "-", "")}storage"
  resource_group_name      = azurerm_resource_group.rg.name
  location                 = azurerm_resource_group.rg.location
  account_tier             = "Standard"
  account_replication_type = "LRS"
  min_tls_version          = "TLS1_2"
  
  network_rules {
    default_action             = "Deny"
    virtual_network_subnet_ids = [azurerm_subnet.app_subnet.id]
    bypass                     = ["AzureServices"]
  }
  
  tags = local.tags
}

# Storage Containers
resource "azurerm_storage_container" "data_container" {
  name                  = "data"
  storage_account_name  = azurerm_storage_account.storage.name
  container_access_type = "private"
}

resource "azurerm_storage_container" "logs_container" {
  name                  = "logs"
  storage_account_name  = azurerm_storage_account.storage.name
  container_access_type = "private"
}

# SQL Server
resource "azurerm_mssql_server" "sql_server" {
  name                         = "${local.resource_name_prefix}-sqlserver"
  resource_group_name          = azurerm_resource_group.rg.name
  location                     = azurerm_resource_group.rg.location
  version                      = "12.0"
  administrator_login          = var.sql_admin_username
  administrator_login_password = var.sql_admin_password
  minimum_tls_version          = "1.2"
  
  azuread_administrator {
    login_username = "AzureAD Admin"
    object_id      = data.azurerm_client_config.current.object_id
  }
  
  tags = local.tags
}

# SQL Database
resource "azurerm_mssql_database" "database" {
  name                = "${local.resource_name_prefix}-db"
  server_id           = azurerm_mssql_server.sql_server.id
  collation           = "SQL_Latin1_General_CP1_CI_AS"
  max_size_gb         = 4
  read_scale          = false
  sku_name            = var.environment == "prod" ? "S1" : "Basic"
  zone_redundant      = var.environment == "prod" ? true : false
  
  tags = local.tags
}

# SQL Server Firewall Rules
resource "azurerm_mssql_firewall_rule" "allow_azure_services" {
  name             = "AllowAzureServices"
  server_id        = azurerm_mssql_server.sql_server.id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "0.0.0.0"
}

# SQL Virtual Network Rule
resource "azurerm_mssql_virtual_network_rule" "sql_vnet_rule" {
  name      = "${local.resource_name_prefix}-sql-vnet-rule"
  server_id = azurerm_mssql_server.sql_server.id
  subnet_id = azurerm_subnet.app_subnet.id
}

# Service Bus Namespace
resource "azurerm_servicebus_namespace" "service_bus" {
  name                = "${local.resource_name_prefix}-servicebus"
  location            = azurerm_resource_group.rg.location
  resource_group_name = azurerm_resource_group.rg.name
  sku                 = var.environment == "prod" ? "Standard" : "Basic"
  
  tags = local.tags
}

# Service Bus Queue
resource "azurerm_servicebus_queue" "pipeline_queue" {
  name         = "pipeline-execution-queue"
  namespace_id = azurerm_servicebus_namespace.service_bus.id
  
  enable_partitioning = true
  max_size_in_megabytes = 1024
  default_message_ttl  = "P14D" # 14 days
}

# App Service Plan
resource "azurerm_service_plan" "app_service_plan" {
  name                = "${local.resource_name_prefix}-appplan"
  location            = azurerm_resource_group.rg.location
  resource_group_name = azurerm_resource_group.rg.name
  os_type             = "Windows"
  sku_name            = var.environment == "prod" ? "P1v2" : "B1"
  
  tags = local.tags
}

# App Service
resource "azurerm_windows_web_app" "app_service" {
  name                = "${local.resource_name_prefix}-app"
  location            = azurerm_resource_group.rg.location
  resource_group_name = azurerm_resource_group.rg.name
  service_plan_id     = azurerm_service_plan.app_service_plan.id
  
  https_only = true
  
  site_config {
    always_on        = var.environment == "prod" ? true : false
    ftps_state       = "Disabled"
    min_tls_version  = "1.2"
    
    application_stack {
      dotnet_version = "v9.0"
    }
  }
  
  app_settings = {
    "APPINSIGHTS_INSTRUMENTATIONKEY"        = azurerm_application_insights.app_insights.instrumentation_key
    "APPLICATIONINSIGHTS_CONNECTION_STRING" = azurerm_application_insights.app_insights.connection_string
    "ApplicationInsightsAgent_EXTENSION_VERSION" = "~2"
    
    "ASPNETCORE_ENVIRONMENT"                = var.environment == "prod" ? "Production" : "Development"
    
    "ConnectionStrings__DefaultConnection"  = "@Microsoft.KeyVault(SecretUri=${azurerm_key_vault_secret.db_connection_string.id})"
    "ServiceBus__ConnectionString"          = "@Microsoft.KeyVault(SecretUri=${azurerm_key_vault_secret.servicebus_connection_string.id})"
    "Storage__ConnectionString"             = "@Microsoft.KeyVault(SecretUri=${azurerm_key_vault_secret.storage_connection_string.id})"
    
    "KeyVault__Endpoint"                    = azurerm_key_vault.key_vault.vault_uri
  }
  
  identity {
    type = "SystemAssigned"
  }
  
  tags = local.tags
}

# Key Vault Access Policy for App Service
resource "azurerm_key_vault_access_policy" "app_service" {
  key_vault_id = azurerm_key_vault.key_vault.id
  tenant_id    = azurerm_windows_web_app.app_service.identity[0].tenant_id
  object_id    = azurerm_windows_web_app.app_service.identity[0].principal_id
  
  secret_permissions = [
    "Get", "List",
  ]
}

# Key Vault Secrets
resource "azurerm_key_vault_secret" "db_connection_string" {
  name         = "ConnectionStrings--DefaultConnection"
  value        = "Server=tcp:${azurerm_mssql_server.sql_server.fully_qualified_domain_name},1433;Initial Catalog=${azurerm_mssql_database.database.name};Persist Security Info=False;User ID=${var.sql_admin_username};Password=${var.sql_admin_password};MultipleActiveResultSets=True;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
  key_vault_id = azurerm_key_vault.key_vault.id
  
  depends_on = [
    azurerm_key_vault_access_policy.terraform
  ]
}

resource "azurerm_key_vault_secret" "servicebus_connection_string" {
  name         = "ServiceBus--ConnectionString"
  value        = azurerm_servicebus_namespace.service_bus.default_primary_connection_string
  key_vault_id = azurerm_key_vault.key_vault.id
  
  depends_on = [
    azurerm_key_vault_access_policy.terraform
  ]
}

resource "azurerm_key_vault_secret" "storage_connection_string" {
  name         = "Storage--ConnectionString"
  value        = azurerm_storage_account.storage.primary_connection_string
  key_vault_id = azurerm_key_vault.key_vault.id
  
  depends_on = [
    azurerm_key_vault_access_policy.terraform
  ]
}

# Outputs
output "app_service_url" {
  value = "https://${azurerm_windows_web_app.app_service.default_hostname}"
}

output "sql_server_fqdn" {
  value = azurerm_mssql_server.sql_server.fully_qualified_domain_name
}

output "key_vault_uri" {
  value = azurerm_key_vault.key_vault.vault_uri
}

output "service_bus_namespace" {
  value = azurerm_servicebus_namespace.service_bus.name
}

output "storage_account_name" {
  value = azurerm_storage_account.storage.name
}

output "application_insights_instrumentation_key" {
  value     = azurerm_application_insights.app_insights.instrumentation_key
  sensitive = true
}
