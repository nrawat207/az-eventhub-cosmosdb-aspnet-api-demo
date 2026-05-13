# Azure DevOps CI/CD Setup Guide

This guide walks you through setting up the Azure DevOps pipeline for the Azure EventHub & CosmosDB learning demo.

## Prerequisites

- Azure DevOps project created (Project Settings available)
- Azure Subscription with Contributor access
- Service Principal or Managed Identity with appropriate permissions
- Repository cloned and `azure-pipelines.yml` committed to main branch

## Step 1: Create Azure Service Connection

The pipeline uses a Service Connection to authenticate to Azure without storing credentials.

### Option A: Using Workload Identity Federation (Recommended - No Secrets)

1. **Create Service Principal in Azure:**
   ```bash
   az ad sp create-for-rbac \
     --name "devops-pipeline-sp" \
     --role Contributor \
     --scopes /subscriptions/{SUBSCRIPTION_ID}
   ```
   
   Note the output: `appId`, `tenant` (tenantId), and `objectId`

2. **Configure Workload Identity Federation:**
   ```bash
   # Set variables
   SERVICE_PRINCIPAL_OBJECT_ID="<objectId from above>"
   
   # Create federated credential for Azure DevOps
   az identity federated-credential create \
     --name "devops-$(date +%s)" \
     --identity-name devops-pipeline-identity \
     --issuer "https://dev.azure.com" \
     --subject "org.myorg:project/pipeline-name:ref:refs/heads/main" \
     --resource-group rg-devops
   ```

3. **In Azure DevOps:**
   - Project Settings → Service connections → New service connection
   - Select **Azure Resource Manager**
   - Authentication method: **Workload Identity Federation (Automatic)**
   - Subscription: Select your target subscription
   - Service connection name: `AzureServiceConnection` (or your preference)
   - Grant pipeline access: ✅ Check this box

### Option B: Using Managed Service Identity (If in Azure VM)

1. Assign Managed Identity to the Azure DevOps agent
2. Grant the identity Contributor role on your subscription
3. In Azure DevOps:
   - Project Settings → Service connections → New service connection
   - Select **Azure Resource Manager**
   - Authentication method: **Managed Identity**
   - Service connection name: `AzureServiceConnection`

## Step 2: Create Variable Groups (Pipeline Library)

1. Go to **Pipelines → Library → Variable groups**
2. Create a new variable group named `dev-pipeline-vars`

3. Add the following variables:

| Variable Name | Value | Type |
|---|---|---|
| `AZURE_SUBSCRIPTION_ID` | Your Azure Subscription ID | Plain |
| `AZURE_RESOURCE_GROUP_DEV` | `rg-azlearn-dev` (or your RG name) | Plain |
| `ACR_NAME_DEV` | `azlearnacrdev` (no hyphens) | Plain |
| `ACA_BATCH_APP_NAME` | `batch-processor-api` | Plain |
| `ACA_RECEIVER_APP_NAME` | `progress-receiver-api` | Plain |
| `AZURE_DEVOPS_SERVICE_CONNECTION` | `AzureServiceConnection` | Plain |

4. **Grant pipeline access:**
   - Open the variable group
   - Click **Pipeline permissions** (top right)
   - Add pipeline: `ci-cd-dev` or your pipeline name

## Step 3: Create the Pipeline

### From Azure DevOps Portal:

1. **Pipelines → Create Pipeline**
2. Select your repository
3. **Existing Azure Pipelines YAML file**
   - Path: `azure-pipelines.yml`
   - Branch: `main`
4. Save and run

### From Command Line (Optional):

```bash
az pipelines create \
  --project "YourProjectName" \
  --name "ci-cd-dev" \
  --yaml-path azure-pipelines.yml \
  --repository-type tfsgit \
  --repository YourRepoName
```

## Step 4: Link Variable Group to Pipeline

After creating the pipeline:

1. **Edit the pipeline** (pencil icon)
2. Select **Variables** (top right)
3. Click **Link variable group**
4. Select `dev-pipeline-vars`
5. Click **Link**
6. **Save** the pipeline

## Step 5: Configure Docker Registry Service Connection

For pushing images to ACR:

1. **Project Settings → Service connections → New service connection**
2. Select **Docker Registry**
3. Registry type: **Azure Container Registry**
4. Azure subscription: Select your subscription
5. Azure container registry: `azlearnacrdev` (or your ACR name)
6. Service connection name: `ACRServiceConnection` (or your preference)
7. Grant pipeline access: ✅ Check
8. **Save**

## Step 6: Update Pipeline with Registry Connection

Edit `azure-pipelines.yml` and update the `Build_And_Push_Images` stage:

```yaml
- stage: Build_And_Push_Images
  displayName: 'Build & Push Docker Images'
  dependsOn: Build_And_Test
  condition: and(succeeded(), eq(variables['Build.SourceBranch'], 'refs/heads/main'))
  jobs:
    - job: BuildImages
      displayName: 'Build and push Docker images'
      steps:
        - task: Docker@2
          displayName: 'Build and push BatchProcessor.Api'
          inputs:
            command: 'buildAndPush'
            Dockerfile: 'src/BatchProcessor.Api/Dockerfile'
            repository: '$(REGISTRY_LOGIN_SERVER)/batch-processor'
            tags: |
              $(IMAGE_TAG)
              $(IMAGE_TAG_LATEST)
            containerRegistry: 'ACRServiceConnection'  # <-- Update this
            addPipelineData: true
        
        - task: Docker@2
          displayName: 'Build and push ProgressReceiver.Api'
          inputs:
            command: 'buildAndPush'
            Dockerfile: 'src/ProgressReceiver.Api/Dockerfile'
            repository: '$(REGISTRY_LOGIN_SERVER)/progress-receiver'
            tags: |
              $(IMAGE_TAG)
              $(IMAGE_TAG_LATEST)
            containerRegistry: 'ACRServiceConnection'  # <-- Update this
            addPipelineData: true
```

## Step 7: Set Up Pull Request Validation (Optional)

1. **Repo Settings → Branch policies → main**
2. **Build validation**
   - Click **+** to add build policy
   - Select your pipeline
   - Automatic: ✅ (build on every PR)
   - Policy requirement: **Required** (block merge without passing build)
   - Display name: `ci-cd-dev Build Validation`

## Step 8: Configure Environment Protection Rules (Future)

When adding staging/prod environments:

1. **Pipelines → Environments → New environment**
2. Name: `staging` or `prod`
3. Under the environment, **Approvals and checks → Create approval**
4. Add approvers (users/groups)
5. Business hours: Optional

Then update the pipeline to use the environment:

```yaml
- deployment: DeployToStaging
  displayName: 'Deploy to Staging'
  environment: 'staging'
  strategy:
    runOnce:
      deploy:
        steps:
          - task: AzureCLI@2
            inputs:
              azureSubscription: '$(AZURE_DEVOPS_SERVICE_CONNECTION)'
              # ... deployment steps
```

## Pipeline Structure

The `azure-pipelines.yml` contains **5 stages**:

```
Build_And_Test
├── BuildAndTest (Runs on all branches)

Lint_Bicep (Parallel with Build_And_Test)
├── LintBicep (Runs on all branches)

Build_And_Push_Images (Depends on Build_And_Test)
├── BuildImages (Only on main branch push)

Deploy_Infrastructure (Depends on Lint_Bicep)
├── DeployInfra (Only on main branch push)

Deploy_Apps (Depends on Deploy_Infrastructure + Build_And_Push_Images)
└── DeployApps (Only on main branch push)
```

## Triggers

- **Push to main** → Full pipeline: Build, Lint, Build Images, Deploy Infra, Deploy Apps
- **Pull Request to main** → CI only: Build and Test, Lint Bicep (no deployment)
- **Manual trigger** → Full pipeline on-demand

## Monitoring Pipeline Runs

1. **Pipelines → Runs**
2. Click on a run to see detailed logs
3. **Logs** tab shows each job's execution
4. **Tests** tab shows test results and coverage
5. **Artifacts** tab shows published test results and coverage reports

## Troubleshooting

### "Service connection not found"
- Verify service connection name matches in variable group and pipeline YAML
- Check that pipeline has permission to use the service connection (Library → Variable Groups → Pipeline permissions)

### "Docker build fails: 401 Unauthorized"
- Verify Docker Registry service connection is created and linked
- Check ACR exists and service principal has permissions
- Try: `az acr login --name azlearnacrdev`

### "Bicep build fails"
- Ensure Azure CLI is installed on agent: `az bicep build --file infra/main.bicep`
- Check Bicep syntax: Run locally first

### "Container App not found"
- Verify resource group and app names match variable values
- Ensure Container Apps environment is created by Bicep deployment
- Check: `az containerapp list --resource-group $(AZURE_RESOURCE_GROUP_DEV)`

### "Tests timeout"
- Increase timeout in test task: `timeoutInMinutes: 30`
- Check for hanging tests or database locks

## Best Practices

✅ **Always test locally before pushing** (`dotnet build`, `dotnet test`)  
✅ **Pin .NET version** to avoid surprises from minor updates  
✅ **Use variable groups** for reusable configuration  
✅ **Rotate service principal credentials regularly** (if not using Workload Identity)  
✅ **Enable branch protection** on main (require passing builds)  
✅ **Monitor pipeline costs** (agent time, container builds)  
✅ **Archive test results** for trend analysis  
✅ **Use conditional steps** to skip stages on PR builds  

## Next Steps

1. Commit this setup guide to your repo
2. Follow the steps above to configure Azure DevOps
3. Trigger a manual pipeline run to verify everything works
4. Set up environment protection rules for staging/prod in the future
5. Monitor logs and test results in the Azure DevOps portal

---

**For more info:** [Azure Pipelines Documentation](https://learn.microsoft.com/en-us/azure/devops/pipelines/yaml-schema)
