# Prerequisites

## Software Requirements

### Required Tools

| Tool | Version | Download | Notes |
|------|---------|----------|-------|
| .NET SDK | 10.0+ | [dotnet.microsoft.com](https://dotnet.microsoft.com/download) | Includes runtime, CLI, and build tools |
| Azure CLI | 2.50+ | [Microsoft Docs](https://learn.microsoft.com/en-us/cli/azure/) | For Azure resource management |
| Bicep CLI | Latest | `az bicep install` | Installed via Azure CLI extension |
| Docker Desktop | Latest | [Docker Hub](https://www.docker.com/products/docker-desktop) | For container development & emulators |
| Git | 2.40+ | [git-scm.com](https://git-scm.com/) | Version control |
| VS Code | Latest | [code.visualstudio.com](https://code.visualstudio.com/) | Recommended editor |

### VS Code Extensions (Recommended)

- **C# Dev Kit** — Official C# support (ms-dotnettools.csharp)
- **Bicep** — Bicep language support (Azure.bicep)
- **Docker** — Docker container support (ms-azuretools.vscode-docker)
- **Azure Tools** — Azure CLI and resource management (ms-vscode.vscode-azuretools)
- **REST Client** — Test APIs locally (humao.rest-client)

### Optional Tools

- **Postman** or **Insomnia** — GUI for API testing
- **Azure Storage Explorer** — Browse Blob/CosmosDB emulator data
- **CosmosDB Emulator** — Native Windows emulator (alternative to Docker)

## Azure Subscription Requirements

### Permissions Needed

You need **Contributor** role (or equivalent) on an Azure subscription to:
- Create resource groups
- Deploy Bicep templates
- Create Container Apps, EventHub, CosmosDB, Key Vault, networking resources
- Configure role assignments (RBAC)

### Estimated Monthly Cost (Dev)

For a development environment running continuously:
- **Container Apps**: ~$15-25/mo (1 vCPU, 2GB RAM per app)
- **EventHub** (Standard, 1 TU): ~$10/mo
- **CosmosDB** (serverless): ~$0.50-3/mo (depends on usage)
- **Key Vault**: ~$0.60/mo
- **Storage** (checkpointing): <$1/mo
- **Container Registry**: ~$5/mo
- **Log Analytics**: ~$0.50-2/mo (depends on data volume)

**Total: ~$32-50/month** for basic continuous deployment

💡 **Tip**: Use dev/test subscriptions if available, or delete resources after learning.

## Initial Setup Steps

### 1. Install Prerequisites

```bash
# Verify .NET 10 installation
dotnet --version  # Should output 10.x.x

# Verify Azure CLI
az --version

# Install Bicep
az bicep install

# Verify Docker
docker --version
```

### 2. Configure Azure CLI

```bash
# Log in to Azure
az login

# List your subscriptions
az account list --output table

# Set default subscription (replace <SUBSCRIPTION_ID>)
az account set --subscription <SUBSCRIPTION_ID>

# Verify current context
az account show
```

### 3. Clone Repository

```bash
git clone <your-repo-url>
cd az-eventhub-cosmosdb-aspnet-api-demo
```

### 4. Verify .NET Build

```bash
# Restore dependencies
dotnet restore

# Build solution
dotnet build

# Run tests
dotnet test
```

### 5. (Optional) Configure GitHub for CI/CD

If using GitHub Actions:

```bash
# Log in to GitHub CLI
gh auth login

# Create repository (if not already done)
gh repo create az-eventhub-cosmosdb-aspnet-api-demo --public

# Configure OIDC for GitHub Actions (see docs/04-cicd-setup.md)
```

## Verify Your Environment

Run this script to validate all prerequisites:

```bash
#!/bin/bash
set -e

echo "🔍 Checking prerequisites..."

# Check .NET
if ! command -v dotnet &> /dev/null; then
    echo "❌ .NET SDK not found"
    exit 1
fi
DOTNET_VERSION=$(dotnet --version)
echo "✅ .NET $DOTNET_VERSION installed"

# Check Azure CLI
if ! command -v az &> /dev/null; then
    echo "❌ Azure CLI not found"
    exit 1
fi
echo "✅ Azure CLI installed"

# Check Docker
if ! command -v docker &> /dev/null; then
    echo "❌ Docker not found"
    exit 1
fi
echo "✅ Docker installed"

# Check Git
if ! command -v git &> /dev/null; then
    echo "❌ Git not found"
    exit 1
fi
echo "✅ Git installed"

# Check Azure login
if ! az account show &> /dev/null; then
    echo "⚠️  Not logged into Azure. Run: az login"
else
    CURRENT_SUB=$(az account show --query name -o tsv)
    echo "✅ Logged into Azure subscription: $CURRENT_SUB"
fi

echo "🎉 All prerequisites verified!"
```

## Next Steps

→ [Local Setup](02-local-setup.md)
