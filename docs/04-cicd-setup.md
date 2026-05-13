# CI/CD Setup Guide

This project uses **Azure DevOps Pipelines** for CI/CD automation. For detailed setup instructions, see [06 - Azure DevOps Setup](06-azure-devops-setup.md).

## Overview

The CI/CD pipeline has these stages:

1. **Build & Test** — Run on all PRs and pushes
2. **Lint Bicep** — Validate infrastructure code
3. **Build & Push Images** — Create Docker images, push to Container Registry (main branch only)
4. **Deploy Infrastructure** — Run Bicep deployment (main branch only)
5. **Deploy Applications** — Update ACA apps with new images (main branch only)

## Azure DevOps Setup

For comprehensive step-by-step instructions on setting up Azure DevOps Pipelines, see [Azure DevOps Setup Guide](06-azure-devops-setup.md).

### Quick Summary

1. Create **Service Connection** (Workload Identity Federation recommended)
2. Create **Variable Group** in Pipeline Library with required variables
3. Link variable group to pipeline
4. Create **Docker Registry Service Connection** for ACR
5. Configure **Pull Request validation** (optional)
6. Run pipeline manually to verify setup

The pipeline file `azure-pipelines.yml` is already configured and ready to use.

## Pipeline Triggers

| Trigger | Stages |
|---------|--------|
| **Push to main** | Build → Lint → Build Images → Deploy Infra → Deploy Apps |
| **Pull Request to main** | Build → Lint only (no deployment) |
| **Manual trigger** | Full pipeline on-demand |

## Monitoring Pipeline Runs

### Azure DevOps Portal

1. Go to **Pipelines → Runs**
2. Click on a run to view details
3. **Logs** tab shows each job's execution
4. **Tests** tab shows test results and coverage
5. **Artifacts** tab contains published reports

### Sample Commands

```bash
# View recent pipeline runs
az pipelines runs list \
  --project "YourProjectName" \
  --pipeline-name "ci-cd-dev" \
  --top 10

# View logs from latest run
az pipelines runs show \
  --project "YourProjectName" \
  --id <run-id> \
  --open
```

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

### Service Connection Not Found

**Error**: `Service connection not found`

**Solution**:
- Verify service connection name in variable group matches `AZURE_DEVOPS_SERVICE_CONNECTION`
- Check pipeline has permission to use the service connection
- Go to Library → Variable Groups → Pipeline permissions

### OIDC Authentication Fails

**Error**: `AADSTS700016: Application ... not found in directory`

**Solution**:
- Verify `AZURE_CLIENT_ID` matches the service principal
- Check federated credential issuer is correct
- Ensure service principal has Contributor role on subscription

### Docker Build Fails

**Error**: `unauthorized: authentication required`

**Solution**:
```bash
# Verify Docker Registry service connection
# Check ACR exists and service principal has AcrPush role
az role assignment list \
  --assignee $AZURE_CLIENT_ID \
  --query "[?contains(roleDefinitionName, 'Acr')].roleDefinitionName" -o table
```

### Container Apps Update Fails

**Error**: `InvalidRequest: Image pull failed`

**Solution**:
- Verify image was pushed to ACR: `az acr repository list -n azlearnacrdev`
- Check ACA managed identity has `AcrPull` role on ACR
- Ensure ACA is on the same VNet if using private endpoints

## Next Steps

→ [Architecture Deep Dive](05-architecture.md)
