environment = "dev"
location = "East US"
resource_group_name = "rg-dataprocessingservice"
app_name = "dataprocessingservice"
sql_admin_username = "sqladmin"
# sql_admin_password should be provided via environment variable or CI/CD pipeline
# sql_admin_password = "StrongPassword123!"

# Development environment specific overrides
allowed_ip_addresses = [
  # Add development team IP addresses here
  # "203.0.113.1",
  # "203.0.113.2"
]
