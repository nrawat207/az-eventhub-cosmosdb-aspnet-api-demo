# Architecture Deep Dive

This document explores the design decisions, patterns, and best practices used throughout this project.

## Why This Architecture?

### Event-Driven Decoupling

**Problem**: If BatchProcessor and ProgressReceiver directly communicated:
- Tight coupling → hard to scale independently
- One failure cascades to the other
- Difficult to replay messages

**Solution**: EventHub as a message broker
- ✅ Decouples producers from consumers
- ✅ Enables replay of historical events (retention)
- ✅ Scales independently (partitions)
- ✅ High throughput (millions of events/second)

### NoSQL for Document-Centric State

**Why CosmosDB NoSQL (not SQL Database)?**

| Aspect | SQL Database | CosmosDB NoSQL |
|--------|-------------|----------------|
| Schema | Rigid (must pre-define columns) | Flexible (documents are JSON) |
| Scaling | Vertical (bigger servers) | Horizontal (more partitions) |
| Global Distribution | Complex | Built-in |
| Query Language | T-SQL | SQL/MongoDB/Cassandra |
| Partition Key | Implicit (by design) | Explicit (you choose) |

**Choice**: CosmosDB NoSQL because:
- Batch jobs are document-centric (one document per job)
- Status changes frequently → flexible schema nice
- Global distribution ready for future expansion
- Partition key (JobId) makes scaling trivial

### Managed Identity (Zero Secrets)

**Without Managed Identity:**
```csharp
// ❌ Secrets in code/config
var connection = "DefaultEndpointProtocol=https;AccountName=...;AccountKey=xyz123...";
```

**With Managed Identity:**
```csharp
// ✅ No secrets anywhere
var client = new EventHubProducerClient(
  new Uri("https://myNamespace.servicebus.windows.net"),
  new DefaultAzureCredential()  // Uses system-assigned identity
);
```

**Benefits:**
- No connection string rotation
- Auditable via RBAC
- No accidental leaks in logs
- Works across environments (dev uses local emulators, prod uses real identities)

### Private VNet Isolation

**Architecture choice**: All resources on private network with no public internet access

```
    ┌─────────────────────────────────┐
    │    Azure Subscription           │
    │  ┌───────────────────────────┐  │
    │  │   Virtual Network         │  │
    │  │   10.0.0.0/16            │  │
    │  │                          │  │
    │  │  ┌─────────────┐         │  │
    │  │  │  ACA Env    │         │  │
    │  │  │ (no public) │         │  │
    │  │  └─────────────┘         │  │
    │  │          ▲               │  │
    │  │          │               │  │
    │  │  ┌───────┼───────┐       │  │
    │  │  │   Private     │       │  │
    │  │  │  Endpoints    │       │  │
    │  │  │ (to services) │       │  │
    │  │  └───────────────┘       │  │
    │  │                          │  │
    │  └───────────────────────────┘  │
    │                                 │
    │  External PaaS                  │
    │  (Key Vault, EventHub,          │
    │   CosmosDB) inside VNet         │
    └─────────────────────────────────┘
    
    🚫 No direct internet access
    ✅ All traffic within Azure backbone
```

**Why?**
- **Security**: No exposure to DDoS, bots
- **Compliance**: Meets HIPAA, PCI-DSS requirements (no internet exposure)
- **Cost**: No NAT Gateway charges
- **Performance**: Uses Azure backbone instead of public internet

## Design Patterns Used

### 1. Producer-Consumer Pattern

**Producer** (BatchProcessor):
```csharp
public class EventHubPublisherService
{
    public async Task PublishAsync(BatchProgressEvent evt)
    {
        using var eventData = new EventData(JsonSerializer.SerializeToUtf8Bytes(evt));
        await producerClient.SendEventAsync(eventData);
    }
}
```

**Consumer** (ProgressReceiver):
```csharp
public class EventHubConsumerService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await processorClient.StartProcessingAsync(ct);
        // OnProcessEventAsync called for each message
    }
}
```

### 2. Retry Pattern with Exponential Backoff

Transient failures (network hiccup, service throttling) should retry:

```csharp
private async Task<T> RetryAsync<T>(Func<Task<T>> operation, int maxRetries = 3)
{
    for (int attempt = 0; attempt < maxRetries; attempt++)
    {
        try
        {
            return await operation();
        }
        catch (OperationCanceledException) { throw; }  // Don't retry cancellation
        catch
        {
            if (attempt == maxRetries - 1) throw;
            await Task.Delay((int)Math.Pow(2, attempt) * 1000);  // 1s, 2s, 4s
        }
    }
}
```

### 3. Background Service Pattern

Long-running background tasks should be `BackgroundService`:

```csharp
public class BatchJobService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var job = await channel.Reader.ReadAsync(stoppingToken);
            await ProcessJobAsync(job, stoppingToken);
        }
    }
}
```

**Benefits:**
- Graceful shutdown (respects `stoppingToken`)
- Integrated with ASP.NET Core host lifecycle
- Can register multiple background services

### 4. Partition Key Design

CosmosDB requires explicit partition key choice:

```csharp
public class BatchJob
{
    [JsonPropertyName("id")]
    public string Id { get; set; }  // Unique document ID (usually same as JobId)
    
    [JsonPropertyName("jobId")]
    public string JobId { get; set; }  // Partition key value
    
    // ... other properties
}

// Container definition
container = database.CreateContainerIfNotExistsAsync("BatchJobs", "/jobId");
```

**Why JobId?**
- ✅ Queries are often `SELECT * WHERE jobId = X`
- ✅ Natural document grouping (one batch = one partition)
- ✅ Enables parallel processing (scale replicas → more partitions)
- ✅ Balanced cardinality (many jobs, not too skewed)

## Observability Strategy

### Structured Logging

All logs are JSON for easy parsing:

```json
{
  "timestamp": "2026-05-12T10:00:00Z",
  "level": "Information",
  "logger": "BatchProcessor.Api.Services.BatchJobService",
  "jobId": "550e8400-e29b-41d4-a716-446655440000",
  "message": "Batch job started",
  "properties": {
    "totalItems": 1000,
    "nodeId": "pod-abc123"
  }
}
```

**Parsed in**: Log Analytics → Kusto queries → alerts

### Application Insights Custom Telemetry

Beyond auto-collected metrics, manually track:

```csharp
telemetryClient.TrackEvent("BatchJobCompleted", 
  properties: new Dictionary<string, string> { { "jobId", jobId } },
  metrics: new Dictionary<string, double> { { "durationSeconds", duration.TotalSeconds } }
);
```

### Health Probes

ACA uses health probes to restart unhealthy containers:

```csharp
// Simple GET /health endpoint
app.MapGet("/health", () => Results.Ok("Healthy"));
```

**ACA configuration**:
```bicep
livenessProbe {
  httpGet {
    path: "/health"
    port: 8080
  }
  initialDelaySeconds: 10
  periodSeconds: 30
}
```

## Performance Considerations

### EventHub Partitioning

Batch 4 partitions to scale processing:

```bicep
eventHubProperties: {
  messageRetentionInDays: 1
  partitionCount: 4  // Up to 4 consumers in parallel
  captureDescription: {
    enabled: false
  }
}
```

**Throughput**: ~1,000 events/sec per partition (more with standard tier)

### CosmosDB Throughput

Serverless CosmosDB for dev (no minimum spend):

```bicep
'Microsoft.DocumentDB/databaseAccounts@2023-04-15': {
  capacity: {
    totalThroughputLimit: 4000  // Max RU/s shared across all operations
  }
}
```

**For higher load** → Switch to provisioned throughput (e.g., 10,000 RU/s)

### ACA Scaling

Scale based on HTTP/Queue metrics:

```bicep
scale: {
  minReplicas: 1      // Always have at least one
  maxReplicas: 3      // Limit to 3 for dev cost control
  rules: [
    {
      name: "http-scaling"
      custom: {
        metadata: {
          desiredReplicas: "2"
          targetQueryPerSecond: "1000"
        }
      }
    }
  ]
}
```

## Security Best Practices Implemented

| Practice | Implementation |
|----------|-----------------|
| **No Secrets in Code** | ✅ Key Vault + Managed Identity |
| **No Public Endpoints** | ✅ Private VNet, private endpoints |
| **RBAC Least Privilege** | ✅ Each component gets minimal required roles |
| **Network Isolation** | ✅ NSGs, subnet delegation, private endpoints |
| **Encryption in Transit** | ✅ TLS for all services |
| **Soft Delete** | ✅ Key Vault, CosmosDB point-in-time restore |
| **Audit Logging** | ✅ Application Insights, Azure Monitor |

## Production Hardening Checklist

Before moving to production, implement:

- [ ] CosmosDB backup policy (continuous backup)
- [ ] EventHub geo-replication to secondary region
- [ ] Application Insights alerting (high latency, errors)
- [ ] Key Vault purge protection enabled
- [ ] Network Security Group review (deny all, allow specific)
- [ ] Managed Identity role review (least privilege)
- [ ] Container image scanning (security vulnerabilities)
- [ ] Rate limiting on APIs
- [ ] CORS configured properly
- [ ] Request logging/audit for compliance

## References

- [EventHub Documentation](https://learn.microsoft.com/en-us/azure/event-hubs/)
- [CosmosDB NoSQL Modeling](https://learn.microsoft.com/en-us/azure/cosmos-db/sql/modeling-data)
- [Azure Container Apps](https://learn.microsoft.com/en-us/azure/container-apps/)
- [Azure Security Benchmark](https://learn.microsoft.com/en-us/security/benchmark/azure/)

---

**Next**: Deploy and run the system!
