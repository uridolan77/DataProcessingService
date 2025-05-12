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
  retention_in_days   = var.log_retention_days[var.environment]
  tags                = local.tags
}

# Get current client configuration
data "azurerm_client_config" "current" {}

# SQL Database Module
module "sql_database" {
  source = "./modules/sql-database"
  
  resource_group_name = azurerm_resource_group.rg.name
  location            = azurerm_resource_group.rg.location
  server_name         = "${local.resource_name_prefix}-sqlserver"
  database_name       = "${local.resource_name_prefix}-db"
  admin_username      = var.sql_admin_username
  admin_password      = var.sql_admin_password
  sku_name            = var.sql_database_sku[var.environment]
  max_size_gb         = 4
  zone_redundant      = var.environment == "prod" ? true : false
  subnet_id           = azurerm_subnet.app_subnet.id
  
  allowed_ip_addresses = [
    for ip in var.allowed_ip_addresses : {
      name             = "AllowedIP-${index(var.allowed_ip_addresses, ip)}"
      start_ip_address = ip
      end_ip_address   = ip
    }
  ]
  
  tags = local.tags
}

# Key Vault Module
module "key_vault" {
  source = "./modules/key-vault"
  
  resource_group_name = azurerm_resource_group.rg.name
  location            = azurerm_resource_group.rg.location
  key_vault_name      = "${local.resource_name_prefix}-kv"
  sku_name            = "standard"
  subnet_ids          = [azurerm_subnet.app_subnet.id]
  
  access_policies = []
  
  tags = local.tags
}

# Storage Account Module
module "storage_account" {
  source = "./modules/storage-account"
  
  resource_group_name      = azurerm_resource_group.rg.name
  location                 = azurerm_resource_group.rg.location
  storage_account_name     = "${replace(local.resource_name_prefix, "-", "")}storage"
  account_tier             = "Standard"
  account_replication_type = var.storage_account_replication[var.environment]
  account_kind             = "StorageV2"
  access_tier              = "Hot"
  subnet_ids               = [azurerm_subnet.app_subnet.id]
  
  containers = [
    {
      name                  = "data"
      container_access_type = "private"
    },
    {
      name                  = "logs"
      container_access_type = "private"
    }
  ]
  
  tags = local.tags
}

# Service Bus Module
module "service_bus" {
  source = "./modules/service-bus"
  
  resource_group_name = azurerm_resource_group.rg.name
  location            = azurerm_resource_group.rg.location
  namespace_name      = "${local.resource_name_prefix}-servicebus"
  sku                 = var.service_bus_sku[var.environment]
  capacity            = var.environment == "prod" ? 1 : 0
  zone_redundant      = var.environment == "prod" ? true : false
  subnet_ids          = [azurerm_subnet.app_subnet.id]
  
  queues = [
    {
      name                       = "pipeline-execution-queue"
      enable_partitioning        = true
      max_size_in_megabytes      = 1024
      default_message_ttl        = "P14D"
      dead_lettering_on_message_expiration = true
      duplicate_detection_history_time_window = "PT10M"
      max_delivery_count         = 10
    }
  ]
  
  topics = [
    {
      name                       = "pipeline-events"
      enable_partitioning        = true
      max_size_in_megabytes      = 1024
      default_message_ttl        = "P14D"
      subscriptions              = [
        {
          name                     = "pipeline-completed"
          max_delivery_count       = 10
          default_message_ttl      = "P14D"
          dead_lettering_on_message_expiration = true
          filter_type              = "SqlFilter"
          sql_filter               = "eventType = 'PipelineCompleted'"
        },
        {
          name                     = "pipeline-failed"
          max_delivery_count       = 10
          default_message_ttl      = "P14D"
          dead_lettering_on_message_expiration = true
          filter_type              = "SqlFilter"
          sql_filter               = "eventType = 'PipelineFailed'"
        }
      ]
    }
  ]
  
  tags = local.tags
}

# App Service Module
module "app_service" {
  source = "./modules/app-service"
  
  resource_group_name    = azurerm_resource_group.rg.name
  location               = azurerm_resource_group.rg.location
  app_service_name       = "${local.resource_name_prefix}-app"
  app_service_plan_name  = "${local.resource_name_prefix}-appplan"
  sku_name               = var.app_service_sku[var.environment]
  always_on              = var.environment == "prod" ? true : false
  subnet_id              = azurerm_subnet.app_subnet.id
  
  app_settings = {
    "APPINSIGHTS_INSTRUMENTATIONKEY"        = azurerm_application_insights.app_insights.instrumentation_key
    "APPLICATIONINSIGHTS_CONNECTION_STRING" = azurerm_application_insights.app_insights.connection_string
    "ApplicationInsightsAgent_EXTENSION_VERSION" = "~2"
    
    "ASPNETCORE_ENVIRONMENT"                = var.environment == "prod" ? "Production" : "Development"
    
    "KeyVault__Endpoint"                    = module.key_vault.key_vault_uri
  }
  
  tags = local.tags
}

# Key Vault Access Policy for App Service
resource "azurerm_key_vault_access_policy" "app_service" {
  key_vault_id = module.key_vault.key_vault_id
  tenant_id    = module.app_service.tenant_id
  object_id    = module.app_service.principal_id
  
  secret_permissions = [
    "Get", "List",
  ]
}

# Key Vault Access Policy for Terraform
resource "azurerm_key_vault_access_policy" "terraform" {
  key_vault_id = module.key_vault.key_vault_id
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

# Key Vault Secrets
resource "azurerm_key_vault_secret" "db_connection_string" {
  name         = "ConnectionStrings--DefaultConnection"
  value        = module.sql_database.connection_string
  key_vault_id = module.key_vault.key_vault_id
  
  depends_on = [
    azurerm_key_vault_access_policy.terraform
  ]
}

resource "azurerm_key_vault_secret" "servicebus_connection_string" {
  name         = "ServiceBus--ConnectionString"
  value        = module.service_bus.default_primary_connection_string
  key_vault_id = module.key_vault.key_vault_id
  
  depends_on = [
    azurerm_key_vault_access_policy.terraform
  ]
}

resource "azurerm_key_vault_secret" "storage_connection_string" {
  name         = "Storage--ConnectionString"
  value        = module.storage_account.primary_connection_string
  key_vault_id = module.key_vault.key_vault_id
  
  depends_on = [
    azurerm_key_vault_access_policy.terraform
  ]
}
