#!/bin/bash

#
# setup-keyvault-secrets.sh
# 
# Sets up Azure Key Vault secrets required by the application.
# This script accepts parameters and populates Key Vault with connection strings
# and endpoints for CosmosDB, EventHub, and Application Insights.
#
# Usage:
#   ./setup-keyvault-secrets.sh \
#     --resource-group <RG_NAME> \
#     --key-vault-name <KV_NAME> \
#     --cosmos-endpoint <COSMOS_ENDPOINT> \
#     --eventhub-fqdn <EVENTHUB_FQDN> \
#     --appinsights-connection-string <APPINSIGHTS_CONNSTR>
#

set -euo pipefail

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
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

# Initialize variables
RESOURCE_GROUP=""
KEY_VAULT_NAME=""
COSMOS_ENDPOINT=""
EVENTHUB_NAMESPACE_FQDN=""
APPINSIGHTS_CONNECTION_STRING=""

# Parse command-line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --resource-group)
            RESOURCE_GROUP="$2"
            shift 2
            ;;
        --key-vault-name)
            KEY_VAULT_NAME="$2"
            shift 2
            ;;
        --cosmos-endpoint)
            COSMOS_ENDPOINT="$2"
            shift 2
            ;;
        --eventhub-fqdn)
            EVENTHUB_NAMESPACE_FQDN="$2"
            shift 2
            ;;
        --appinsights-connection-string)
            APPINSIGHTS_CONNECTION_STRING="$2"
            shift 2
            ;;
        *)
            print_error "Unknown option: $1"
            exit 1
            ;;
    esac
done

# Validate required parameters
if [[ -z "$RESOURCE_GROUP" || -z "$KEY_VAULT_NAME" || -z "$COSMOS_ENDPOINT" || \
      -z "$EVENTHUB_NAMESPACE_FQDN" || -z "$APPINSIGHTS_CONNECTION_STRING" ]]; then
    print_error "Missing required parameters"
    echo "Usage:"
    echo "  ./setup-keyvault-secrets.sh \\"
    echo "    --resource-group <RG_NAME> \\"
    echo "    --key-vault-name <KV_NAME> \\"
    echo "    --cosmos-endpoint <COSMOS_ENDPOINT> \\"
    echo "    --eventhub-fqdn <EVENTHUB_FQDN> \\"
    echo "    --appinsights-connection-string <APPINSIGHTS_CONNSTR>"
    exit 1
fi

print_info "Setting up Key Vault secrets..."
print_info "Resource Group: $RESOURCE_GROUP"
print_info "Key Vault: $KEY_VAULT_NAME"
echo ""

# Counter for success/failure
SECRETS_SET=0
SECRETS_FAILED=0

# Secret 1: CosmosDB Account Endpoint
# Purpose: Used by ProgressReceiver.Api to connect to CosmosDB for reading/writing batch job status
print_info "Setting secret: CosmosDb--AccountEndpoint"
if az keyvault secret set \
    --name "CosmosDb--AccountEndpoint" \
    --value "$COSMOS_ENDPOINT" \
    --vault-name "$KEY_VAULT_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    > /dev/null 2>&1; then
    print_success "CosmosDb--AccountEndpoint"
    ((SECRETS_SET++))
else
    print_error "Failed to set CosmosDb--AccountEndpoint"
    ((SECRETS_FAILED++))
fi

# Secret 2: EventHub Namespace FQDN
# Purpose: Used by BatchProcessor.Api to connect to EventHub for publishing batch progress events
print_info "Setting secret: EventHub--NamespaceFQDN"
if az keyvault secret set \
    --name "EventHub--NamespaceFQDN" \
    --value "$EVENTHUB_NAMESPACE_FQDN" \
    --vault-name "$KEY_VAULT_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    > /dev/null 2>&1; then
    print_success "EventHub--NamespaceFQDN"
    ((SECRETS_SET++))
else
    print_error "Failed to set EventHub--NamespaceFQDN"
    ((SECRETS_FAILED++))
fi

# Secret 3: Application Insights Connection String
# Purpose: Used by both APIs for instrumenting telemetry and application insights tracking
print_info "Setting secret: ApplicationInsights--ConnectionString"
if az keyvault secret set \
    --name "ApplicationInsights--ConnectionString" \
    --value "$APPINSIGHTS_CONNECTION_STRING" \
    --vault-name "$KEY_VAULT_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    > /dev/null 2>&1; then
    print_success "ApplicationInsights--ConnectionString"
    ((SECRETS_SET++))
else
    print_error "Failed to set ApplicationInsights--ConnectionString"
    ((SECRETS_FAILED++))
fi

echo ""
print_info "Secret setup summary:"
print_success "$SECRETS_SET secret(s) successfully set"
if [[ $SECRETS_FAILED -gt 0 ]]; then
    print_error "$SECRETS_FAILED secret(s) failed"
    exit 1
fi

print_success "All secrets have been successfully configured in Key Vault!"
