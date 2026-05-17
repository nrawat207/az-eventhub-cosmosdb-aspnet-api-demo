# First-Time Setup & CI/CD Deployment Guide

## Overview

This guide explains how to handle the two-stage deployment process:
1. **First-Time Setup**: Deploy infrastructure without container apps
2. **Build & Deploy**: Build Docker images and deploy container apps (CI/CD ready)

## Problem Solved

The original deployment failed with this error:
```
MANIFEST_UNKNOWN: manifest tagged by "dev" is not found
```

This happens because container images don't exist in Azure Container Registry (ACR) yet on first deployment. The solution makes container app deployment optional, allowing you to:
- Deploy all infrastructure (VNETs, ACR, CosmosDB, EventHub, etc.) first
- Build and push container images to ACR
- Then deploy container apps with proper image references

## First-Time Setup (Infrastructure Only)

### Step 1: Deploy Infrastructure
```bash
cd infra/scripts
./deploy.sh --environment dev --location westindia
```

When prompted about deploying container apps:
```
ℹ First-time setup detected: Container images don't exist in ACR yet

You have two options:
1. Deploy infrastructure ONLY (without container apps) - Recommended for first-time setup
2. Deploy infrastructure WITH container apps (requires images to already exist in ACR)

Deploy container apps now? (y/n, default: n):
```

**Answer `n`** to deploy infrastructure only.

### Step 2: Build Docker Images

From the project root:
```bash
docker build -t azlearnacrdev.azurecr.io/batchprocessor-api:dev src/BatchProcessor.Api
docker build -t azlearnacrdev.azurecr.io/progressreceiver-api:dev src/ProgressReceiver.Api
```

Or use the provided build-and-push script (see next section).

### Step 3: Push to ACR

```bash
# Login to ACR (this will prompt you for credentials)
az acr login --name azlearnacrdev

# Push images
docker push azlearnacrdev.azurecr.io/batchprocessor-api:dev
docker push azlearnacrdev.azurecr.io/progressreceiver-api:dev
```

### Step 4: Deploy Container Apps

After images are pushed to ACR:
```bash
az deployment group create \
  --name "azlearn-deploy-apps-$(date +%s)" \
  --resource-group rg-eventhub-cosmosdb-dev \
  --template-file infra/main.bicep \
  --parameters infra/parameters/dev.bicepparam \
  --parameters deployContainerApps=true
```

## Automated Build & Push (CI/CD Ready)

Use the provided `build-and-push.sh` script to automate building and pushing:

```bash
bash infra/scripts/build-and-push.sh \
  --environment dev \
  --acr-name azlearnacrdev \
  --image-tag dev
```

This script:
- ✓ Verifies Docker is running
- ✓ Verifies Azure CLI authentication
- ✓ Builds both Docker images
- ✓ Logs in to ACR
- ✓ Pushes images to ACR
- ✓ Provides next steps for deploying container apps

## Configuration Changes

### Modified Files

1. **infra/main.bicep**
   - Added `deployContainerApps` parameter (default: `true`)
   - Made container app modules conditional: `module <name> '...' = if (deployContainerApps) { ... }`
   - Updated outputs to handle conditional deployments

2. **infra/parameters/dev.bicepparam**
   - Added `param deployContainerApps = true`
   - Can override with `--parameters deployContainerApps=false` for infrastructure-only

3. **infra/scripts/deploy.sh**
   - Added logic to detect first-time setup
   - Prompts user to choose between infrastructure-only or full deployment
   - Updated "Next Steps" to provide environment-specific instructions
   - Supports `--parameters deployContainerApps=<true|false>`

4. **infra/scripts/build-and-push.sh** (NEW)
   - Automated build and push script for CI/CD
   - Handles Docker image building
   - Logs in to ACR and pushes images
   - Provides next steps for deployment

## Common Workflows

### Local Development: First Time

```bash
# 1. Deploy infrastructure
./infra/scripts/deploy.sh --environment dev

# Answer 'n' when asked about container apps

# 2. Build and push images
./infra/scripts/build-and-push.sh --environment dev --acr-name azlearnacrdev

# 3. Deploy container apps
az deployment group create \
  --name "azlearn-deploy-apps-$(date +%s)" \
  --resource-group rg-eventhub-cosmosdb-dev \
  --template-file infra/main.bicep \
  --parameters infra/parameters/dev.bicepparam \
  --parameters deployContainerApps=true
```

### CI/CD Pipeline

```bash
# Step 1: Infrastructure deployment (one-time)
./infra/scripts/deploy.sh --environment dev

# Answer 'n' for infrastructure-only

# Step 2: Build, push, and deploy (runs on each commit)
./infra/scripts/build-and-push.sh --environment dev --acr-name azlearnacrdev

# Step 3: Deploy/update container apps
az deployment group create \
  --name "azlearn-deploy-apps-$(date +%s)" \
  --resource-group rg-eventhub-cosmosdb-dev \
  --template-file infra/main.bicep \
  --parameters infra/parameters/dev.bicepparam \
  --parameters deployContainerApps=true \
  --parameters batchProcessorImage="azlearnacrdev.azurecr.io/batchprocessor-api:$(git rev-parse --short HEAD)" \
  --parameters progressReceiverImage="azlearnacrdev.azurecr.io/progressreceiver-api:$(git rev-parse --short HEAD)"
```

### Subsequent Deployments

Once infrastructure is set up and container apps exist:

```bash
# Build and push new images
./infra/scripts/build-and-push.sh --environment dev --acr-name azlearnacrdev

# Update container apps with new images
az containerapp update --name azlearn-batchprocessor-dev \
  --resource-group rg-eventhub-cosmosdb-dev \
  --image azlearnacrdev.azurecr.io/batchprocessor-api:dev

az containerapp update --name azlearn-progressreceiver-dev \
  --resource-group rg-eventhub-cosmosdb-dev \
  --image azlearnacrdev.azurecr.io/progressreceiver-api:dev
```

## Parameters Reference

### deployContainerApps

Controls whether container apps are deployed:
- `true` (default): Deploy container apps using specified images
- `false`: Skip container apps (infrastructure-only)

**Usage:**
```bash
--parameters deployContainerApps=false
```

### Image Parameters

Override default image references:
```bash
--parameters batchProcessorImage="<acr>.azurecr.io/batchprocessor-api:<tag>"
--parameters progressReceiverImage="<acr>.azurecr.io/progressreceiver-api:<tag>"
```

## Troubleshooting

### Deployment validation fails
```bash
# Validate template before deploying
az deployment group validate \
  --resource-group rg-eventhub-cosmosdb-dev \
  --template-file infra/main.bicep \
  --parameters infra/parameters/dev.bicepparam \
  --parameters deployContainerApps=false
```

### Docker build fails
- Ensure Docker is running: `docker ps`
- Check dockerfile paths are correct relative to project root
- Verify .NET SDK is installed: `dotnet --version`

### ACR login fails
```bash
# Re-authenticate
az login
az acr login --name azlearnacrdev
```

### Container app deployment fails
- Check images exist in ACR: `az acr repository list --name azlearnacrdev`
- Verify managed identity has ACR pull permissions
- Check container app logs: `az containerapp logs show --name <app-name> --resource-group <rg>`

## Next Steps

- **Monitor Deployments**: Check Application Insights for app health
- **View Logs**: Use Azure Monitor to analyze application traces
- **Scale Apps**: Adjust `minReplicas`/`maxReplicas` in parameters
- **Update Code**: Rebuild images and push to ACR for new deployments
