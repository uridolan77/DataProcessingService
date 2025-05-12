variable "resource_group_name" {
  description = "The name of the resource group"
  type        = string
}

variable "location" {
  description = "The Azure region where resources will be created"
  type        = string
}

variable "namespace_name" {
  description = "The name of the Service Bus Namespace"
  type        = string
}

variable "sku" {
  description = "The SKU of the Service Bus Namespace"
  type        = string
  default     = "Standard"
}

variable "capacity" {
  description = "The capacity of the Service Bus Namespace"
  type        = number
  default     = 0
}

variable "zone_redundant" {
  description = "Whether the Service Bus Namespace should be zone redundant"
  type        = bool
  default     = false
}

variable "queues" {
  description = "A list of queues to create in the Service Bus Namespace"
  type        = list(object({
    name                       = string
    enable_partitioning        = bool
    max_size_in_megabytes      = number
    default_message_ttl        = string
    dead_lettering_on_message_expiration = bool
    duplicate_detection_history_time_window = string
    max_delivery_count         = number
  }))
  default     = []
}

variable "topics" {
  description = "A list of topics to create in the Service Bus Namespace"
  type        = list(object({
    name                       = string
    enable_partitioning        = bool
    max_size_in_megabytes      = number
    default_message_ttl        = string
    subscriptions              = list(object({
      name                     = string
      max_delivery_count       = number
      default_message_ttl      = string
      dead_lettering_on_message_expiration = bool
      filter_type              = string
      sql_filter               = string
    }))
  }))
  default     = []
}

variable "subnet_ids" {
  description = "A list of subnet IDs to allow access to the Service Bus Namespace"
  type        = list(string)
  default     = []
}

variable "tags" {
  description = "A map of tags to apply to the resources"
  type        = map(string)
  default     = {}
}

resource "azurerm_servicebus_namespace" "namespace" {
  name                = var.namespace_name
  location            = var.location
  resource_group_name = var.resource_group_name
  sku                 = var.sku
  capacity            = var.capacity
  zone_redundant      = var.zone_redundant
  
  tags = var.tags
}

resource "azurerm_servicebus_namespace_network_rule_set" "network_rules" {
  count                         = length(var.subnet_ids) > 0 ? 1 : 0
  namespace_id                  = azurerm_servicebus_namespace.namespace.id
  default_action                = "Deny"
  
  network_rules {
    subnet_id                   = var.subnet_ids[count.index]
    ignore_missing_vnet_service_endpoint = false
  }
}

resource "azurerm_servicebus_queue" "queues" {
  for_each                      = { for queue in var.queues : queue.name => queue }
  
  name                          = each.value.name
  namespace_id                  = azurerm_servicebus_namespace.namespace.id
  
  enable_partitioning           = each.value.enable_partitioning
  max_size_in_megabytes         = each.value.max_size_in_megabytes
  default_message_ttl           = each.value.default_message_ttl
  dead_lettering_on_message_expiration = each.value.dead_lettering_on_message_expiration
  duplicate_detection_history_time_window = each.value.duplicate_detection_history_time_window
  max_delivery_count            = each.value.max_delivery_count
}

resource "azurerm_servicebus_topic" "topics" {
  for_each                      = { for topic in var.topics : topic.name => topic }
  
  name                          = each.value.name
  namespace_id                  = azurerm_servicebus_namespace.namespace.id
  
  enable_partitioning           = each.value.enable_partitioning
  max_size_in_megabytes         = each.value.max_size_in_megabytes
  default_message_ttl           = each.value.default_message_ttl
}

resource "azurerm_servicebus_subscription" "subscriptions" {
  for_each = {
    for subscription in flatten([
      for topic in var.topics : [
        for sub in topic.subscriptions : {
          topic_name = topic.name
          name       = sub.name
          max_delivery_count = sub.max_delivery_count
          default_message_ttl = sub.default_message_ttl
          dead_lettering_on_message_expiration = sub.dead_lettering_on_message_expiration
          filter_type = sub.filter_type
          sql_filter  = sub.sql_filter
        }
      ]
    ]) : "${subscription.topic_name}-${subscription.name}" => subscription
  }
  
  name                          = each.value.name
  topic_id                      = azurerm_servicebus_topic.topics[each.value.topic_name].id
  max_delivery_count            = each.value.max_delivery_count
  default_message_ttl           = each.value.default_message_ttl
  dead_lettering_on_message_expiration = each.value.dead_lettering_on_message_expiration
}

resource "azurerm_servicebus_subscription_rule" "rules" {
  for_each = {
    for subscription in flatten([
      for topic in var.topics : [
        for sub in topic.subscriptions : {
          topic_name = topic.name
          name       = sub.name
          filter_type = sub.filter_type
          sql_filter  = sub.sql_filter
        } if sub.filter_type != null && sub.sql_filter != null
      ]
    ]) : "${subscription.topic_name}-${subscription.name}" => subscription
  }
  
  name                          = "${each.value.name}-rule"
  subscription_id               = azurerm_servicebus_subscription.subscriptions["${each.value.topic_name}-${each.value.name}"].id
  filter_type                   = each.value.filter_type
  sql_filter                    = each.value.sql_filter
}

output "namespace_id" {
  description = "The ID of the Service Bus Namespace"
  value       = azurerm_servicebus_namespace.namespace.id
}

output "namespace_name" {
  description = "The name of the Service Bus Namespace"
  value       = azurerm_servicebus_namespace.namespace.name
}

output "default_primary_connection_string" {
  description = "The primary connection string for the Service Bus Namespace"
  value       = azurerm_servicebus_namespace.namespace.default_primary_connection_string
  sensitive   = true
}

output "default_secondary_connection_string" {
  description = "The secondary connection string for the Service Bus Namespace"
  value       = azurerm_servicebus_namespace.namespace.default_secondary_connection_string
  sensitive   = true
}

output "queue_ids" {
  description = "The IDs of the Service Bus Queues"
  value       = { for name, queue in azurerm_servicebus_queue.queues : name => queue.id }
}

output "topic_ids" {
  description = "The IDs of the Service Bus Topics"
  value       = { for name, topic in azurerm_servicebus_topic.topics : name => topic.id }
}
