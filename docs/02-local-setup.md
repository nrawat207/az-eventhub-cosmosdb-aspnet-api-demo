# Local Development Setup

This guide walks through running the entire system locally using Docker Compose emulators (Azurite, CosmosDB Emulator, EventHub Emulator).

## Prerequisites

✅ [Complete prerequisites first](01-prerequisites.md)

## Quick Start (5 minutes)

```bash
# 1. Clone and navigate
git clone <repo-url>
cd az-eventhub-cosmosdb-aspnet-api-demo

# 2. Start emulators
docker-compose up -d

# 3. Build and test
dotnet build
dotnet test

# 4. Start APIs in VS Code debug mode (F5)
# Or run from terminal:
dotnet run --project src/BatchProcessor.Api &
dotnet run --project src/ProgressReceiver.Api &

# 5. Send test request
curl -X POST http://localhost:5001/api/batch/start \
  -H "Content-Type: application/json" \
  -d '{"jobName": "Demo Job", "totalItems": 5}'

# 6. Check status
curl http://localhost:5002/api/jobs
```

## Detailed Setup

### 1. Start Emulators

```bash
# Start all emulators (Storage, CosmosDB, EventHub simulation)
docker-compose up -d

# Wait 30 seconds for emulators to initialize
sleep 30

# Verify containers are running
docker ps
```

**Ports used:**
- **Azurite**: 10000 (Blob), 10001 (Queue), 10002 (Table)
- **CosmosDB Emulator**: 8081 (HTTPS)
- **EventHub/Kafka emulator**: 9092 (if using Kafka bridge)

### 2. Configure Local Environment

Create `.env.local` in the root:

```bash
# Copy the template
cp .env.example .env.local

# Edit with your values (mostly defaults work locally)
# Key variables:
# - AZURE_KEY_VAULT_URI="" (disabled locally)
# - EventHub__NamespaceFQDN="localhost:9092" (or emulator endpoint)
# - CosmosDb__AccountEndpoint="https://localhost:8081"
```

### 3. Configure appsettings.Development.json

Both projects already have development settings. Verify:

**BatchProcessor.Api/appsettings.Development.json:**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  },
  "EventHub": {
    "NamespaceFQDN": "localhost:9092"
  }
}
```

**ProgressReceiver.Api/appsettings.Development.json:**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  },
  "CosmosDb": {
    "AccountEndpoint": "https://localhost:8081",
    "AccountKey": "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTjZx0jTKk+YPvqqO08IfHNcSomM7ZH0MsoZo0="
  }
}
```

### 4. Restore and Build

```bash
# Restore NuGet packages
dotnet restore

# Build solution (should complete with 0 errors, 0 warnings)
dotnet build

# (Optional) Run unit tests
dotnet test
```

### 5. Run Both APIs

**Option A: Debug in VS Code**

1. Open VS Code: `code .`
2. Navigate to `.vscode/launch.json` (should be pre-configured)
3. Set breakpoints as needed
4. Press `F5` to start both APIs with debugging

**Option B: Terminal**

```bash
# Terminal 1: Start BatchProcessor.Api
cd src/BatchProcessor.Api
dotnet run --environment Development
# Should output: listening on http://localhost:5001

# Terminal 2: Start ProgressReceiver.Api
cd src/ProgressReceiver.Api
dotnet run --environment Development
# Should output: listening on http://localhost:5002
```

### 6. Test the System

Use REST Client VS Code extension or curl:

**Start a batch job:**
```bash
curl -X POST http://localhost:5001/api/batch/start \
  -H "Content-Type: application/json" \
  -d '{
    "jobName": "My First Batch",
    "totalItems": 10
  }'

# Response: 202 Accepted
# Header: Location: /api/batch/start
# Body: {"jobId": "some-guid-here"}
```

**Check batch status:**
```bash
# Replace {jobId} with the response from above
curl http://localhost:5001/api/batch/{jobId}/status

# Response: 200 OK
# Body: { "jobId": "...", "status": "Running", "processedItems": 5, ... }
```

**List all jobs from CosmosDB:**
```bash
curl http://localhost:5002/api/jobs

# Response: 200 OK
# Body: [ { "jobId": "...", ... }, ... ]
```

**Health checks:**
```bash
# Both should return 200 OK
curl http://localhost:5001/health
curl http://localhost:5002/health
```

## Using requests.http (REST Client)

The repository includes `requests.http` with pre-configured requests:

1. Open `requests.http` in VS Code
2. Install "REST Client" extension (humao.rest-client)
3. Click **Send Request** above each request to execute

## Debugging Tips

### Emulator Issues

**CosmosDB won't start:**
```bash
# Clean and restart
docker-compose down -v
docker-compose up -d

# Wait 60 seconds for emulator to initialize
```

**EventHub emulator not responding:**
- Check Docker logs: `docker logs <container-id>`
- Verify network: `docker network ls`
- Rebuild: `docker-compose build --no-cache`

### Application Issues

**Connection refused errors:**
```bash
# Verify emulators are running
docker ps | grep -E "cosmosdb|azurite|kafka"

# Check network connectivity
curl -v https://localhost:8081 2>&1 | head -20
```

**Missing emulator certificates:**
- CosmosDB Emulator uses self-signed certs
- C# client should auto-trust in dev mode
- If issues persist, configure: `new CosmosClientOptions { AllowBulkExecution = true }`

### Structured Logging

Both APIs log JSON by default (development) and console format (production).

View logs:
```bash
# Tail logs from terminal output
# Or check Application Insights once deployed
```

## Cleanup

```bash
# Stop emulators
docker-compose down

# (Optional) Remove emulator data volumes
docker-compose down -v
```

## Next Steps

→ [Deploy Infrastructure to Azure](03-infra-deployment.md)
