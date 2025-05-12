#!/bin/bash

# DataProcessingService Infrastructure Deployment Script

# Usage:
# ./deploy.sh <environment> [action]
# environment: dev, test, or prod
# action: plan (default), apply, destroy

set -e

# Check if environment is provided
if [ -z "$1" ]; then
  echo "Error: Environment not specified"
  echo "Usage: ./deploy.sh <environment> [action]"
  echo "  environment: dev, test, or prod"
  echo "  action: plan (default), apply, destroy"
  exit 1
fi

# Set variables
ENVIRONMENT=$1
ACTION=${2:-plan}
VALID_ENVIRONMENTS=("dev" "test" "prod")
VALID_ACTIONS=("plan" "apply" "destroy")

# Validate environment
if [[ ! " ${VALID_ENVIRONMENTS[@]} " =~ " ${ENVIRONMENT} " ]]; then
  echo "Error: Invalid environment '$ENVIRONMENT'"
  echo "Valid environments: dev, test, prod"
  exit 1
fi

# Validate action
if [[ ! " ${VALID_ACTIONS[@]} " =~ " ${ACTION} " ]]; then
  echo "Error: Invalid action '$ACTION'"
  echo "Valid actions: plan, apply, destroy"
  exit 1
fi

# Check if SQL admin password is set
if [ -z "$TF_VAR_sql_admin_password" ]; then
  echo "Error: SQL admin password not set"
  echo "Please set the TF_VAR_sql_admin_password environment variable"
  echo "Example: export TF_VAR_sql_admin_password='YourStrongPassword123!'"
  exit 1
fi

# Initialize Terraform
echo "Initializing Terraform..."
terraform init \
  -backend-config="resource_group_name=tfstate" \
  -backend-config="storage_account_name=$TF_STORAGE_ACCOUNT" \
  -backend-config="container_name=tfstate" \
  -backend-config="key=dataprocessingservice-${ENVIRONMENT}.tfstate"

# Execute the requested action
case $ACTION in
  plan)
    echo "Planning Terraform deployment for $ENVIRONMENT environment..."
    terraform plan -var-file=${ENVIRONMENT}.tfvars -out=${ENVIRONMENT}.tfplan
    ;;
  apply)
    echo "Applying Terraform deployment for $ENVIRONMENT environment..."
    terraform apply ${ENVIRONMENT}.tfplan
    ;;
  destroy)
    echo "WARNING: You are about to destroy the $ENVIRONMENT environment infrastructure!"
    echo "This action cannot be undone. Please type the environment name to confirm:"
    read -r confirmation
    if [ "$confirmation" != "$ENVIRONMENT" ]; then
      echo "Confirmation failed. Aborting."
      exit 1
    fi
    echo "Destroying Terraform deployment for $ENVIRONMENT environment..."
    terraform destroy -var-file=${ENVIRONMENT}.tfvars
    ;;
esac

echo "Done!"
