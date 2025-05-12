# DataProcessingService Infrastructure

This directory contains the infrastructure as code (IaC) for the DataProcessingService microservice.

## Overview

The DataProcessingService requires several Azure resources to function properly:

- **App Service**: Hosts the DataProcessingService API
- **SQL Database**: Stores application data
- **Key Vault**: Securely stores secrets
- **Storage Account**: Stores data files
- **Service Bus**: Messaging for asynchronous processing
- **Application Insights**: Application monitoring
- **Log Analytics**: Centralized logging
- **Virtual Network**: Network isolation for the application

## Infrastructure as Code

We use Terraform to define and provision the infrastructure. The Terraform configuration is organized as follows:

- **terraform/**: Contains the Terraform configuration files
  - **main.tf**: Main Terraform configuration
  - **main_modular.tf**: Modular version of the Terraform configuration
  - **variables.tf**: Variable definitions
  - **outputs.tf**: Output definitions
  - **dev.tfvars**: Development environment variables
  - **test.tfvars**: Test environment variables
  - **prod.tfvars**: Production environment variables
  - **modules/**: Reusable Terraform modules
    - **app-service/**: App Service module
    - **key-vault/**: Key Vault module
    - **service-bus/**: Service Bus module
    - **sql-database/**: SQL Database module
    - **storage-account/**: Storage Account module

## Environments

The infrastructure is designed to support multiple environments:

- **Development (dev)**: Used for development and testing
- **Test (test)**: Used for integration testing and QA
- **Production (prod)**: Used for production workloads

Each environment has its own configuration file (*.tfvars) with environment-specific settings.

## Deployment

The infrastructure can be deployed using the provided scripts:

- **deploy.sh**: Bash script for Linux/macOS
- **deploy.ps1**: PowerShell script for Windows

See the [terraform/README.md](terraform/README.md) file for detailed deployment instructions.

## CI/CD Integration

The infrastructure deployment is integrated with GitHub Actions. The workflow is defined in `.github/workflows/terraform.yml`.

## Network Architecture

The infrastructure uses a Virtual Network with separate subnets for the application and database. This provides network isolation and security.

## Security Considerations

- All sensitive data is stored in Azure Key Vault
- Network access is restricted using Virtual Network integration
- Managed identities are used for authentication between services
- TLS 1.2 is enforced for all communications
- SQL Server is protected with firewall rules

## Monitoring and Logging

- Application Insights is used for application monitoring
- Log Analytics is used for centralized logging
- Metrics and logs are retained according to environment-specific retention policies

## Cost Optimization

- Development and test environments use lower-tier SKUs
- Production environment uses higher-tier SKUs with redundancy
- Resources are sized appropriately for each environment

## Disaster Recovery

- Production environment uses zone-redundant storage
- Backups are configured for the SQL Database
- Service Bus uses Standard tier in production for redundancy

## Compliance

- All resources are tagged for better organization and cost tracking
- Resource naming follows a consistent pattern
- Infrastructure is versioned and changes are tracked in Git
