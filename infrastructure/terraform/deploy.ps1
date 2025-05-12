# DataProcessingService Infrastructure Deployment Script

# Usage:
# .\deploy.ps1 -Environment <environment> [-Action <action>]
# environment: dev, test, or prod
# action: plan (default), apply, destroy

param (
    [Parameter(Mandatory=$true)]
    [ValidateSet("dev", "test", "prod")]
    [string]$Environment,
    
    [Parameter(Mandatory=$false)]
    [ValidateSet("plan", "apply", "destroy")]
    [string]$Action = "plan"
)

# Check if SQL admin password is set
if (-not $env:TF_VAR_sql_admin_password) {
    Write-Error "Error: SQL admin password not set"
    Write-Host "Please set the TF_VAR_sql_admin_password environment variable"
    Write-Host "Example: `$env:TF_VAR_sql_admin_password = 'YourStrongPassword123!'"
    exit 1
}

# Initialize Terraform
Write-Host "Initializing Terraform..." -ForegroundColor Cyan
terraform init `
  -backend-config="resource_group_name=tfstate" `
  -backend-config="storage_account_name=$env:TF_STORAGE_ACCOUNT" `
  -backend-config="container_name=tfstate" `
  -backend-config="key=dataprocessingservice-${Environment}.tfstate"

# Execute the requested action
switch ($Action) {
    "plan" {
        Write-Host "Planning Terraform deployment for $Environment environment..." -ForegroundColor Cyan
        terraform plan -var-file="${Environment}.tfvars" -out="${Environment}.tfplan"
    }
    "apply" {
        Write-Host "Applying Terraform deployment for $Environment environment..." -ForegroundColor Cyan
        terraform apply "${Environment}.tfplan"
    }
    "destroy" {
        Write-Host "WARNING: You are about to destroy the $Environment environment infrastructure!" -ForegroundColor Red
        Write-Host "This action cannot be undone. Please type the environment name to confirm:" -ForegroundColor Red
        $confirmation = Read-Host
        if ($confirmation -ne $Environment) {
            Write-Host "Confirmation failed. Aborting." -ForegroundColor Red
            exit 1
        }
        Write-Host "Destroying Terraform deployment for $Environment environment..." -ForegroundColor Red
        terraform destroy -var-file="${Environment}.tfvars"
    }
}

Write-Host "Done!" -ForegroundColor Green
