# DataProcessingService Infrastructure

This directory contains Terraform configuration for provisioning the infrastructure required by the DataProcessingService.

## Infrastructure Components

The Terraform configuration provisions the following Azure resources:

- **Resource Group**: Contains all resources
- **Virtual Network**: Network isolation for the application
- **App Service**: Hosts the DataProcessingService API
- **SQL Database**: Stores application data
- **Key Vault**: Securely stores secrets
- **Storage Account**: Stores data files
- **Service Bus**: Messaging for asynchronous processing
- **Application Insights**: Application monitoring
- **Log Analytics**: Centralized logging

## Prerequisites

- [Terraform](https://www.terraform.io/downloads.html) (v1.0.0 or newer)
- [Azure CLI](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli) (v2.30.0 or newer)
- Azure subscription with appropriate permissions

## Getting Started

1. **Login to Azure**

   ```bash
   az login
   ```

2. **Select Azure Subscription**

   ```bash
   az account set --subscription "Your Subscription Name or ID"
   ```

3. **Initialize Terraform**

   ```bash
   terraform init
   ```

## Deployment

### Development Environment

```bash
# Set SQL admin password as an environment variable
export TF_VAR_sql_admin_password="YourStrongPassword123!"

# Plan the deployment
terraform plan -var-file=dev.tfvars -out=dev.tfplan

# Apply the deployment
terraform apply dev.tfplan
```

### Test Environment

```bash
# Set SQL admin password as an environment variable
export TF_VAR_sql_admin_password="YourStrongPassword123!"

# Plan the deployment
terraform plan -var-file=test.tfvars -out=test.tfplan

# Apply the deployment
terraform apply test.tfplan
```

### Production Environment

```bash
# Set SQL admin password as an environment variable
export TF_VAR_sql_admin_password="YourStrongPassword123!"

# Plan the deployment
terraform plan -var-file=prod.tfvars -out=prod.tfplan

# Apply the deployment
terraform apply prod.tfplan
```

## Remote State

For production use, it's recommended to configure Terraform to use remote state storage. This can be done by uncommenting and configuring the `backend` block in `main.tf`.

First, create the storage account for Terraform state:

```bash
# Create resource group for Terraform state
az group create --name tfstate --location eastus

# Create storage account for Terraform state
az storage account create --name tfstate$RANDOM --resource-group tfstate --sku Standard_LRS --encryption-services blob

# Create container for Terraform state
az storage container create --name tfstate --account-name tfstate$RANDOM
```

Then update the `backend` configuration in `main.tf` with the appropriate values.

## CI/CD Integration

This Terraform configuration can be integrated with CI/CD pipelines. Here's an example of how to use it with Azure DevOps:

1. Store the Terraform state in Azure Storage
2. Use Azure DevOps variable groups to manage environment-specific variables
3. Create a pipeline that runs Terraform init, plan, and apply

Example pipeline YAML:

```yaml
trigger:
  branches:
    include:
    - main
  paths:
    include:
    - infrastructure/terraform/*

pool:
  vmImage: 'ubuntu-latest'

variables:
- group: dataprocessingservice-$(environment)

steps:
- task: AzureCLI@2
  displayName: 'Terraform Init'
  inputs:
    azureSubscription: 'Your-Azure-Service-Connection'
    scriptType: 'bash'
    scriptLocation: 'inlineScript'
    inlineScript: |
      cd infrastructure/terraform
      terraform init \
        -backend-config="resource_group_name=tfstate" \
        -backend-config="storage_account_name=$(tf_storage_account)" \
        -backend-config="container_name=tfstate" \
        -backend-config="key=dataprocessingservice-$(environment).tfstate"

- task: AzureCLI@2
  displayName: 'Terraform Plan'
  inputs:
    azureSubscription: 'Your-Azure-Service-Connection'
    scriptType: 'bash'
    scriptLocation: 'inlineScript'
    inlineScript: |
      cd infrastructure/terraform
      terraform plan \
        -var-file=$(environment).tfvars \
        -var="sql_admin_password=$(sql_admin_password)" \
        -out=$(environment).tfplan

- task: AzureCLI@2
  displayName: 'Terraform Apply'
  condition: succeeded()
  inputs:
    azureSubscription: 'Your-Azure-Service-Connection'
    scriptType: 'bash'
    scriptLocation: 'inlineScript'
    inlineScript: |
      cd infrastructure/terraform
      terraform apply $(environment).tfplan
```

## Cleanup

To destroy the infrastructure when it's no longer needed:

```bash
terraform destroy -var-file=dev.tfvars
```

**Note**: Be extremely careful with the `destroy` command, especially in production environments.

## Security Considerations

- Sensitive values like passwords should never be stored in the Terraform files
- Use Azure Key Vault to store secrets
- Restrict network access to resources
- Use managed identities for authentication between services
- Regularly rotate credentials
- Enable diagnostic logs for all resources
