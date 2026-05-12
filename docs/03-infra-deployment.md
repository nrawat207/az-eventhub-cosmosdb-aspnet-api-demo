# Infrastructure Deployment

This guide walks through deploying all Azure resources required for the dev environment using Bicep Infrastructure as Code.

## Prerequisites

✅ [Prerequisites](01-prerequisites.md) completed  
✅ Azure subscription with Contributor access  
✅ Default Azure CLI subscription set  

## Architecture Overview

The Bicep templates in `/infra` provision:

- **Networking**: Virtual Network, subnets, Network Security Groups, private endpoints
- **Security**: Key Vault with soft delete and purge protection
- **Data**: EventHub namespace and CosmosDB account
- **Storage**: Azure Storage for EventHub checkpointing
- **Compute**: Container Apps environment and two ACA apps
- **Observability**: Application Insights and Log Analytics Workspace
- **Container Registry**: For storing Docker images

## One-Time Azure Setup

### 1. Create Resource Group

```bash
# Set variables
RESOURCE_GROUP="rg-eventhub-cosmosdb-dev"
LOCATION="eastus"  # Choose a region near you

# Create resource group
az group create \
  --name $RESOURCE_GROUP \
  --location $LOCATION

# Verify
az group show --name $RESOURCE_GROUP
```

### 2. Validate Bicep Templates

```bash
# Build Bicep files (ensures syntax is correct)
az bicep build --file infra/main.bicep

# Output: C:\...\infra\main.json (ARM template)
```

### 3. Preview Deployment (What-If)

```bash
# See what will be created without actually creating it
az deployment group what-if \
  --name "deploy-eventhub-cosmos-dev" \
  --resource-group $RESOURCE_GROUP \
  --template-file infra/main.bicep \
  --parameters infra/parameters/dev.bicepparam

# Review the output before proceeding
```

## Deploy Infrastructure

### 4. Run Bicep Deployment

```bash
# Deploy all resources
az deployment group create \
  --name "deploy-eventhub-cosmos-dev" \
  --resource-group $RESOURCE_GROUP \
  --template-file infra/main.bicep \
  --parameters infra/parameters/dev.bicepparam \
  --query "properties.outputs" \
  --output table

# Capture important outputs (save these!)
# - keyVaultName
# - containerRegistryName
# - acrLoginServer
# - cosmosDbAccountName
# - eventHubNamespaceId
```

**Expected duration:** 8-15 minutes

### 5. Configure Key Vault Secrets

Once infrastructure is deployed, populate Key Vault with connection details:

```bash
# Get output values from previous deployment
KEY_VAULT_NAME=$(az deployment group show \
  --name "deploy-eventhub-cosmos-dev" \
  --resource-group $RESOURCE_GROUP \
  --query "properties.outputs.keyVaultName.value" -o tsv)

# Get secret values from deployed resources
COSMOS_ENDPOINT=$(az cosmosdb show \
  --resource-group $RESOURCE_GROUP \
  --name "azlearn-cosmos-dev" \
  --query "documentEndpoint" -o tsv)

EVENTHUB_NAMESPACE=$(az eventhubs namespace show \
  --resource-group $RESOURCE_GROUP \
  --name "azlearn-evhns-dev" \
  --query "name" -o tsv)

# Store secrets in Key Vault
az keyvault secret set \
  --vault-name $KEY_VAULT_NAME \
  --name "CosmosDb--AccountEndpoint" \
  --value $COSMOS_ENDPOINT

az keyvault secret set \
  --vault-name $KEY_VAULT_NAME \
  --name "EventHub--NamespaceFQDN" \
  --value "${EVENTHUB_NAMESPACE}.servicebus.windows.net"

# Verify secrets were set
az keyvault secret list --vault-name $KEY_VAULT_NAME --query "[].name" -o table
```

## Verify Deployment

### 6. Check All Resources

```bash
# List all resources in the resource group
az resource list \
  --resource-group $RESOURCE_GROUP \
  --query "[].{Name:name, Type:type}" \
  -o table

# Expected resources: ~15-20 items
```

### 7. Test Networking (Private Endpoints)

```bash
# Verify private endpoints were created
az network private-endpoint list \
  --resource-group $RESOURCE_GROUP \
  -o table

# Verify NSG rules
az network nsg list \
  --resource-group $RESOURCE_GROUP \
  -o table
```

### 8. Access Application Insights

```bash
# Get Application Insights name
APPINSIGHTS_NAME=$(az deployment group show \
  --name "deploy-eventhub-cosmos-dev" \
  --resource-group $RESOURCE_GROUP \
  --query "properties.outputs.applicationInsightsName.value" -o tsv)

# View in Azure Portal
echo "https://portal.azure.com/#@microsoft.onmicrosoft.com/resource/subscriptions/.../providers/microsoft.insights/components/$APPINSIGHTS_NAME"

# Or via CLI
az monitor app-insights show --name $APPINSIGHTS_NAME --resource-group $RESOURCE_GROUP
```

## Cost Analysis

### Estimated Monthly Cost

| Resource | SKU | Est. Cost |
|----------|-----|-----------|
| Container Apps (2x) | 1vCPU, 2GB | $20-30 |
| EventHub | Standard, 1 TU | $10 |
| CosmosDB | Serverless | $0.50-5 |
| Storage Account | Standard LRS | <$1 |
| Key Vault | Standard | $0.60 |
| Container Registry | Basic | $5 |
| Log Analytics | Pay-as-you-go | $0.50-2 |
| **Total (dev)** | | **~$37-54** |

💡 **Optimization tips:**
- Use CosmosDB serverless for low-traffic scenarios
- Set budget alerts: `az costmanagement alert create`
- Delete non-essential resources after testing

## Teardown (Delete All Resources)

⚠️ **Warning**: This deletes everything. Use only when done learning.

```bash
# Delete the entire resource group and all resources
az group delete \
  --name $RESOURCE_GROUP \
  --yes  # Skip confirmation

# Verify deletion
az group show --name $RESOURCE_GROUP  # Should fail
```

## Troubleshooting

### Deployment Fails with "Role Assignment Fails"

**Cause**: Managed identity not fully propagated  
**Solution**: Wait 30 seconds and retry

```bash
sleep 30
az deployment group create ... (same command as before)
```

### Private Endpoints Not Resolving

**Cause**: Private DNS Zone not linked to VNet  
**Solution**: Check Bicep template `privatelink.*.azure.com` DNS zones are created

```bash
az network private-dns zone list --resource-group $RESOURCE_GROUP -o table
```

### Key Vault Access Denied

**Cause**: Managed identity not granted Key Vault access  
**Solution**: Verify RBAC role assignments

```bash
az role assignment list \
  --resource-group $RESOURCE_GROUP \
  --query "[].principalName" -o table
```

## Next Steps

→ [Set Up CI/CD](04-cicd-setup.md)
