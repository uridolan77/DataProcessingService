environment = "prod"
location = "East US"
resource_group_name = "rg-dataprocessingservice"
app_name = "dataprocessingservice"
sql_admin_username = "sqladmin"
# sql_admin_password should be provided via environment variable or CI/CD pipeline
# sql_admin_password = "StrongPassword123!"

# Production environment specific overrides
allowed_ip_addresses = [
  # Add production environment IP addresses here
  # "203.0.113.5",
  # "203.0.113.6"
]
