environment = "test"
location = "East US"
resource_group_name = "rg-dataprocessingservice"
app_name = "dataprocessingservice"
sql_admin_username = "sqladmin"
# sql_admin_password should be provided via environment variable or CI/CD pipeline
# sql_admin_password = "StrongPassword123!"

# Test environment specific overrides
allowed_ip_addresses = [
  # Add test environment IP addresses here
  # "203.0.113.3",
  # "203.0.113.4"
]
