# Project Overview

## Purpose

This is a learning project demonstrating a complete, production-grade Azure EventHub + CosmosDB architecture with two cooperating ASP.NET Core APIs, Bicep infrastructure as code, and CI/CD automation.

## High-Level Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    Azure Container Apps                      │
│  ┌──────────────────┐          ┌──────────────────┐         │
│  │ BatchProcessor   │          │  ProgressReceiver│         │
│  │      API         │──EventHub──→      API       │         │
│  │                  │          │                  │         │
│  │ • POST /start    │          │ • GET /jobs      │         │
│  │ • GET /status    │          │ • Consumer loop  │         │
│  └──────────────────┘          └────────┬─────────┘         │
└─────────────────────────────────────────┼──────────────────┘
                                         │
                                         ▼
                                  ┌─────────────┐
                                  │ CosmosDB    │
                                  │ BatchJobs   │
                                  │ Container   │
                                  └─────────────┘
```

## Component Responsibilities

| Component | Technology | Purpose |
|-----------|-----------|---------|
| BatchProcessor.Api | ASP.NET Core 10, ACA | Accepts batch job requests, simulates processing, streams progress events to EventHub |
| ProgressReceiver.Api | ASP.NET Core 10, ACA | Consumes events from EventHub, persists batch job state to CosmosDB |
| Azure EventHub | Event streaming | Decouples the two APIs; enables high-throughput, scalable message delivery |
| Azure CosmosDB | NoSQL database | Persists batch job documents with JobId as partition key |
| Azure Key Vault | Secrets management | Stores connection strings and secrets; accessed via managed identity |
| Virtual Network | Network boundary | Isolates all resources; private endpoints for all PaaS services |
| Container Apps Env | Compute | Hosts both APIs internally (no public ingress) |

## Data Flow

1. **Client sends batch request** → `POST /api/batch/start` to BatchProcessor
2. **BatchProcessor accepts** → Returns 202 Accepted with JobId
3. **BatchProcessor processes** → Simulates work, publishes `BatchProgressEvent` to EventHub after each item
4. **ProgressReceiver consumes** → EventHub processor client delivers messages
5. **ProgressReceiver persists** → Upserts BatchJob document to CosmosDB
6. **Client queries status** → `GET /api/jobs/{jobId}` from ProgressReceiver
7. **ProgressReceiver returns** → Latest batch job status from CosmosDB

## Technology Stack

- **.NET 10** — Latest version of the .NET runtime
- **ASP.NET Core** — Web API framework
- **Azure Managed Identity** — Zero-secret authentication
- **Bicep** — Infrastructure as code language
- **GitHub Actions** — CI/CD automation (Azure DevOps alternative available)
- **Docker** — Container runtime
- **xUnit** — Testing framework

## Learning Goals

After completing this project, you will understand:

✅ How to design event-driven architectures on Azure  
✅ EventHub producer/consumer patterns  
✅ CosmosDB document design and partitioning  
✅ Managed Identity authentication and authorization  
✅ Bicep modular infrastructure patterns  
✅ Private VNets with private endpoints  
✅ CI/CD with GitHub Actions  
✅ Container Apps networking and scaling  
✅ .NET best practices for cloud applications  

## Next Steps

1. Start with [Prerequisites](01-prerequisites.md)
2. Set up your [Local Development Environment](02-local-setup.md)
3. Deploy [Infrastructure](03-infra-deployment.md)
4. Configure [CI/CD](04-cicd-setup.md)
5. Deep dive into [Architecture](05-architecture.md)
