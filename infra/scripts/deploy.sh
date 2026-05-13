#!/bin/bash

#
# deploy.sh
#
# Complete deployment orchestration script for the Azure Learning Demo project.
# This script:
#   1. Creates/validates a resource group
#   2. Deploys Bicep infrastructure to the resource group
#   3. Captures deployment outputs (Key Vault name, ACR name, etc.)
#   4. Calls setup-keyvault-secrets.sh to populate secrets
#   5. Prints a deployment summary
#
# Prerequisites:
#   - Azure CLI installed and authenticated (az login)
#   - Bicep CLI installed (az bicep install)
#   - Bash 4.0+
#
# Usage:
#   ./deploy.sh [--environment dev|staging|prod] [--resource-group-name RG_NAME] [--location eastus]
#
# Environment Variables:
#   AZURE_SUBSCRIPTION_ID  - Override default subscription (optional)
#

set -euo pipefail

# Script directory for calling setup-keyvault-secrets.sh
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Function to print colored output
print_success() {
    echo -e "${GREEN}✓ $1${NC}"
}

print_error() {
    echo -e "${RED}✗ $1${NC}"
}

print_info() {
    echo -e "${YELLOW}ℹ $1${NC}"
}

print_section() {
    echo ""
    echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "${BLUE}$1${NC}"
    echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
}

# Default values
ENVIRONMENT="dev"
RESOURCE_GROUP_NAME=""
LOCATION="eastus"
TEMPLATE_FILE="$PROJECT_ROOT/infra/main.bicep"
PARAMETERS_FILE="$PROJECT_ROOT/infra/parameters/${ENVIRONMENT}.bicepparam"

# Parse command-line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --environment)
            ENVIRONMENT="$2"
            shift 2
            ;;
        --resource-group-name)
            RESOURCE_GROUP_NAME="$2"
            shift 2
            ;;
        --location)
            LOCATION="$2"
            shift 2
            ;;
        *)
            print_error "Unknown option: $1"
            exit 1
            ;;
    esac
done

# Set resource group name based on environment if not provided
if [[ -z "$RESOURCE_GROUP_NAME" ]]; then
    RESOURCE_GROUP_NAME="azlearn-rg-${ENVIRONMENT}"
fi

# Update parameter file path based on environment
PARAMETERS_FILE="$PROJECT_ROOT/infra/parameters/${ENVIRONMENT}.bicepparam"

print_section "Azure Learning Demo - Infrastructure Deployment"
print_info "Environment: $ENVIRONMENT"
print_info "Resource Group: $RESOURCE_GROUP_NAME"
print_info "Location: $LOCATION"
print_info "Template: $TEMPLATE_FILE"
print_info "Parameters: $PARAMETERS_FILE"
echo ""

# Verify files exist
if [[ ! -f "$TEMPLATE_FILE" ]]; then
    print_error "Bicep template not found: $TEMPLATE_FILE"
    exit 1
fi

if [[ ! -f "$PARAMETERS_FILE" ]]; then
    print_error "Parameter file not found: $PARAMETERS_FILE"
    exit 1
fi

print_success "Template and parameter files found"

# Step 1: Check Azure CLI authentication
print_section "Step 1: Verifying Azure CLI Authentication"
if ! az account show > /dev/null 2>&1; then
    print_error "Not authenticated with Azure CLI. Run 'az login' first."
    exit 1
fi

CURRENT_SUBSCRIPTION=$(az account show --query id -o tsv)
print_success "Authenticated with subscription: $CURRENT_SUBSCRIPTION"

# Step 2: Create or validate resource group
print_section "Step 2: Creating/Validating Resource Group"
if az group exists --name "$RESOURCE_GROUP_NAME" --output json | grep -q true; then
    print_info "Resource group '$RESOURCE_GROUP_NAME' already exists"
else
    print_info "Creating resource group: $RESOURCE_GROUP_NAME"
    if az group create --name "$RESOURCE_GROUP_NAME" --location "$LOCATION" > /dev/null; then
        print_success "Resource group created: $RESOURCE_GROUP_NAME"
    else
        print_error "Failed to create resource group"
        exit 1
    fi
fi

# Step 3: Validate Bicep template
print_section "Step 3: Validating Bicep Template"
if az deployment group validate \
    --resource-group "$RESOURCE_GROUP_NAME" \
    --template-file "$TEMPLATE_FILE" \
    --parameters "$PARAMETERS_FILE" \
    > /dev/null 2>&1; then
    print_success "Bicep template validation passed"
else
    print_error "Bicep template validation failed"
    exit 1
fi

# Step 4: Deploy infrastructure
print_section "Step 4: Deploying Infrastructure (this may take 10-15 minutes)"
DEPLOYMENT_NAME="azlearn-deploy-$(date +%s)"

# Deploy and capture output
DEPLOYMENT_OUTPUT=$(az deployment group create \
    --name "$DEPLOYMENT_NAME" \
    --resource-group "$RESOURCE_GROUP_NAME" \
    --template-file "$TEMPLATE_FILE" \
    --parameters "$PARAMETERS_FILE" \
    --output json)

if [[ $? -eq 0 ]]; then
    print_success "Infrastructure deployment completed"
else
    print_error "Infrastructure deployment failed"
    exit 1
fi

# Step 5: Extract deployment outputs
print_section "Step 5: Extracting Deployment Outputs"

# Extract Key Vault name
KEY_VAULT_NAME=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.properties.outputs.keyVaultName.value // "not-found"')
if [[ "$KEY_VAULT_NAME" == "not-found" ]]; then
    print_error "Could not extract Key Vault name from deployment output"
    exit 1
fi
print_success "Key Vault: $KEY_VAULT_NAME"

# Extract ACR name
ACR_NAME=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.properties.outputs.acrName.value // "not-found"')
if [[ "$ACR_NAME" != "not-found" ]]; then
    print_success "Container Registry: $ACR_NAME"
fi

# Extract CosmosDB endpoint
COSMOS_ENDPOINT=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.properties.outputs.cosmosDbEndpoint.value // "not-found"')
if [[ "$COSMOS_ENDPOINT" != "not-found" ]]; then
    print_success "CosmosDB Endpoint: $COSMOS_ENDPOINT"
fi

# Extract EventHub namespace FQDN
EVENTHUB_FQDN=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.properties.outputs.eventHubNamespaceFqdn.value // "not-found"')
if [[ "$EVENTHUB_FQDN" != "not-found" ]]; then
    print_success "EventHub Namespace FQDN: $EVENTHUB_FQDN"
fi

# Extract Application Insights connection string
APPINSIGHTS_CONNSTR=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.properties.outputs.appInsightsConnectionString.value // "not-found"')
if [[ "$APPINSIGHTS_CONNSTR" != "not-found" ]]; then
    print_success "Application Insights Connection String: [REDACTED]"
fi

# Step 6: Set Key Vault secrets
print_section "Step 6: Populating Key Vault Secrets"

# Check if setup-keyvault-secrets.sh exists
if [[ ! -f "$SCRIPT_DIR/setup-keyvault-secrets.sh" ]]; then
    print_error "setup-keyvault-secrets.sh not found at $SCRIPT_DIR/setup-keyvault-secrets.sh"
    exit 1
fi

# Make it executable if not already
chmod +x "$SCRIPT_DIR/setup-keyvault-secrets.sh"

# Call setup-keyvault-secrets.sh with captured values
if bash "$SCRIPT_DIR/setup-keyvault-secrets.sh" \
    --resource-group "$RESOURCE_GROUP_NAME" \
    --key-vault-name "$KEY_VAULT_NAME" \
    --cosmos-endpoint "$COSMOS_ENDPOINT" \
    --eventhub-fqdn "$EVENTHUB_FQDN" \
    --appinsights-connection-string "$APPINSIGHTS_CONNSTR"; then
    print_success "Key Vault secrets configured successfully"
else
    print_error "Failed to configure Key Vault secrets"
    exit 1
fi

# Step 7: Print deployment summary
print_section "Deployment Summary"
cat << EOF
✓ Deployment completed successfully!

Environment Details:
  Environment:              $ENVIRONMENT
  Subscription ID:          $CURRENT_SUBSCRIPTION
  Resource Group:           $RESOURCE_GROUP_NAME
  Location:                 $LOCATION
  Deployment Name:          $DEPLOYMENT_NAME

Key Azure Resources:
  Key Vault:                $KEY_VAULT_NAME
  Container Registry:       $ACR_NAME
  CosmosDB Endpoint:        $COSMOS_ENDPOINT
  EventHub Namespace:       $EVENTHUB_FQDN
  Application Insights:     [Configured]

Next Steps:
  1. Build Docker images:
     docker build -t $ACR_NAME.azurecr.io/batch-processor:dev src/BatchProcessor.Api
     docker build -t $ACR_NAME.azurecr.io/progress-receiver:dev src/ProgressReceiver.Api

  2. Push to ACR:
     az acr login --name $ACR_NAME
     docker push $ACR_NAME.azurecr.io/batch-processor:dev
     docker push $ACR_NAME.azurecr.io/progress-receiver:dev

  3. Deploy container apps:
     az containerapp update --name batch-processor-api --resource-group $RESOURCE_GROUP_NAME \
       --image $ACR_NAME.azurecr.io/batch-processor:dev
     az containerapp update --name progress-receiver-api --resource-group $RESOURCE_GROUP_NAME \
       --image $ACR_NAME.azurecr.io/progress-receiver:dev

  4. View logs:
     az monitor log-analytics query --workspace $(az resource list -g $RESOURCE_GROUP_NAME \
       --query "[?type=='Microsoft.OperationalInsights/workspaces'].name" -o tsv | head -1) \
       --analytics-query "AppTraces | take 100"

  5. Clean up (when done):
     az group delete --name $RESOURCE_GROUP_NAME

EOF

print_success "Deployment script completed!"
