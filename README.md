# Azure EventHub + CosmosDB Learning Demo

A complete end-to-end learning project demonstrating how to build scalable, event-driven applications on Azure using:
- **Azure Container Apps (ACA)** — serverless container hosting
- **Azure EventHub** — high-throughput message streaming
- **Azure CosmosDB** — NoSQL data persistence
- **Azure Key Vault** — secrets management
- **Bicep IaC** — infrastructure as code
- **Azure DevOps Pipelines** — CI/CD automation

## Quick Start

```bash
# Clone and enter the repo
git clone <repo-url>
cd az-eventhub-cosmosdb-aspnet-api-demo

# Local development with emulators
./dev-setup.sh

# Or manual: start emulators via Docker Compose
docker-compose up -d

# Build and test
dotnet build
dotnet test

# Run both APIs
dotnet run --project src/BatchProcessor.Api
# In another terminal:
dotnet run --project src/ProgressReceiver.Api
```

## Architecture

Two ASP.NET Core APIs communicate via Azure EventHub:
1. **BatchProcessor.Api** — Accepts batch jobs, publishes progress events to EventHub
2. **ProgressReceiver.Api** — Consumes events, persists batch state to CosmosDB

All infrastructure is provisioned via Bicep with automated deployment via Azure DevOps Pipelines.

## Documentation

See `/docs` for detailed guides:
- [00 - Overview & Architecture](docs/00-overview.md)
- [01 - Prerequisites](docs/01-prerequisites.md)
- [02 - Local Setup](docs/02-local-setup.md)
- [03 - Infrastructure Deployment](docs/03-infra-deployment.md)
- [04 - CI/CD Setup](docs/04-cicd-setup.md)
- [05 - Architecture Deep Dive](docs/05-architecture.md)
- [06 - Azure DevOps Setup](docs/06-azure-devops-setup.md)

## Prerequisites

- .NET 10 SDK
- Azure CLI & Bicep
- Docker & Docker Compose
- Git
- VS Code (recommended with C#, Bicep, and Docker extensions)

## Project Structure

```
az-eventhub-cosmosdb-aspnet-api-demo/
├── src/
│   ├── BatchProcessor.Api/           # API 1: Batch job processor
│   └── ProgressReceiver.Api/         # API 2: Event consumer
├── tests/
│   ├── BatchProcessor.Api.Tests/
│   └── ProgressReceiver.Api.Tests/
├── infra/                            # Bicep IaC modules
│   ├── modules/
│   ├── main.bicep
│   └── parameters/
├── .github/workflows/                # GitHub Actions CI/CD
├── docs/                             # Comprehensive documentation
└── azure-pipelines.yml               # Azure DevOps alternative
```

## License

MIT — See LICENSE for details

## Contributing

This is a learning project. PRs and feedback welcome!
