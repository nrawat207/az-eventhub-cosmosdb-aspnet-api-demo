# CI/CD Setup Guide

This document walks through configuring GitHub Actions (or Azure DevOps) to automatically build, test, and deploy the application whenever code is pushed.

## Overview

The CI/CD pipeline (GitHub Actions) has these stages:

1. **Build & Test** — Run on all PRs and pushes
2. **Lint Bicep** — Validate infrastructure code
3. **Build & Push Images** — Create Docker images, push to Container Registry
4. **Deploy Infrastructure** — Run Bicep deployment
5. **Deploy Applications** — Update ACA apps with new images

## GitHub Actions Setup

### 1. Prerequisites

- GitHub repository (public or private)
- Azure subscription with Contributor access
- GitHub account with admin access to the repo

### 2. Configure OIDC Federation (Recommended — No Secrets)

GitHub Actions can authenticate to Azure without storing credentials using OpenID Connect (OIDC).

```bash
# Set variables
SUBSCRIPTION_ID=$(az account show --query id -o tsv)
RESOURCE_GROUP="rg-eventhub-cosmosdb-dev"
LOCATION="eastus"

# Create Entra ID app registration for GitHub Actions
APP_ID=$(az ad app create \
  --display-name "github-actions-oidc" \
  --query appId -o tsv)

# Create service principal
OBJECT_ID=$(az ad sp create --id $APP_ID --query id -o tsv)

# Add federated credential for GitHub
az ad app federated-credential create \
  --id $APP_ID \
  --parameters '{
    "name": "github-actions-federated",
    "issuer": "https://token.actions.githubusercontent.com",
    "subject": "repo:YOUR_GITHUB_ORG/az-eventhub-cosmosdb-aspnet-api-demo:ref:refs/heads/main",
    "audiences": ["api://AzureADTokenExchange"]
  }'

# Assign Contributor role
az role assignment create \
  --assignee-object-id $OBJECT_ID \
  --role "Contributor" \
  --scope "/subscriptions/$SUBSCRIPTION_ID"

# Assign Container Registry push role
az role assignment create \
  --assignee-object-id $OBJECT_ID \
  --role "AcrPush" \
  --scope "/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP"

# Note these values for GitHub Secrets
echo "AZURE_CLIENT_ID: $APP_ID"
echo "AZURE_TENANT_ID: $(az account show --query tenantId -o tsv)"
echo "AZURE_SUBSCRIPTION_ID: $SUBSCRIPTION_ID"
```

### 3. Add GitHub Secrets & Variables

Navigate to GitHub repo → **Settings → Secrets and variables → Actions**

**Environment Variables** (no sensitive data):
- `AZURE_RESOURCE_GROUP`: `rg-eventhub-cosmosdb-dev`
- `ACR_NAME`: `azlearnacrdev` (from deployment output)
- `ACA_BATCH_APP_NAME`: `batch-processor-api`
- `ACA_RECEIVER_APP_NAME`: `progress-receiver-api`

**OIDC Credentials** (from above):
- `AZURE_CLIENT_ID`: Application ID from Entra ID
- `AZURE_TENANT_ID`: Your Azure tenant ID
- `AZURE_SUBSCRIPTION_ID`: Your subscription ID

### 4. Configure GitHub Environments (Optional)

For manual approval before deploy to production:

1. Go to **Settings → Environments**
2. Create new environment: `production`
3. Add required reviewers
4. Add secret overrides for prod (e.g., different resource group)

## Pipeline File

The `.github/workflows/ci-cd-dev.yml` file (auto-generated) contains:

### Trigger Conditions

```yaml
on:
  push:
    branches: [main]
  pull_request:
    branches: [main]
  workflow_dispatch:  # Manual trigger
```

### Jobs

```yaml
jobs:
  build-test:
    # Build, test, upload coverage
    
  lint-bicep:
    # Validate all Bicep files
    
  build-push-images:
    # Build Docker images, push to ACR
    needs: [build-test]
    if: github.ref == 'refs/heads/main'
    
  deploy-infra:
    # Run Bicep deployment
    needs: [lint-bicep]
    if: github.ref == 'refs/heads/main'
    
  deploy-apps:
    # Update ACA apps with new images
    needs: [deploy-infra, build-push-images]
    if: github.ref == 'refs/heads/main'
```

## Manual Workflow Dispatch

Trigger pipeline without a commit:

```bash
gh workflow run ci-cd-dev.yml -f environment=dev
```

## Monitoring Deployments

### GitHub Actions UI

1. Go to **Actions** tab in your GitHub repo
2. Click the latest workflow run
3. Expand each job to view logs
4. Check for failures and detailed error messages

### Azure Portal

Monitor deployment progress:

```bash
# Watch deployment in real-time
az deployment group list \
  --resource-group $RESOURCE_GROUP \
  --query "sort_by([].{Name:name, State:properties.provisioningState, Time:properties.timestamp}, &Time)" \
  -o table --watch
```

## Troubleshooting

### OIDC Authentication Fails

**Error**: `AADSTS700016: Application ... not found in directory`

**Solution**:
- Verify `AZURE_CLIENT_ID` matches the app registration
- Check federated credential subject matches your repo path
- Ensure principal has required roles assigned

### Image Push Fails

**Error**: `unauthorized: authentication required`

**Solution**:
```bash
# Verify ACR permissions
az role assignment list \
  --assignee $AZURE_CLIENT_ID \
  --query "[?contains(roleDefinitionName, 'Acr')].roleDefinitionName" -o table
```

### Container Apps Update Fails

**Error**: `InvalidRequest: Image pull failed`

**Solution**:
- Verify image was pushed to ACR: `az acr repository list -n $ACR_NAME`
- Check ACA managed identity has `AcrPull` role on ACR
- Ensure ACA environment is on same VNet as ACR (if private)

## Azure DevOps Alternative

If using Azure DevOps instead of GitHub Actions:

The `azure-pipelines.yml` file (in root) provides equivalent CI/CD pipeline.

### Setup

1. Create Azure DevOps project
2. Create pipeline from `azure-pipelines.yml`
3. Configure service connection using OIDC (similar to above)
4. Set pipeline variables (same as GitHub secrets)

### Syntax

```yaml
trigger:
  - main

pool:
  vmImage: 'ubuntu-latest'

stages:
  - stage: Build
    jobs:
      - job: BuildTest
        # Same build and test steps
        
  - stage: Deploy
    dependsOn: Build
    condition: succeeded()
    jobs:
      - deployment: Infrastructure
        # Bicep deployment
```

## Next Steps

→ [Architecture Deep Dive](05-architecture.md)
