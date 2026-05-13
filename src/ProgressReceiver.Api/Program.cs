using Azure.Core;
using Azure.Identity;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Azure;
using ProgressReceiver.Api.Repositories;
using ProgressReceiver.Api.Services;

var builder = WebApplication.CreateBuilder(args);
var azureCredential = new DefaultAzureCredential();
var keyVaultUri = builder.Configuration["AZURE_KEY_VAULT_URI"];

if (!string.IsNullOrWhiteSpace(keyVaultUri))
{
    builder.Configuration.AddAzureKeyVault(new Uri(keyVaultUri), azureCredential);
}

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(8080);
});

builder.Services.AddSingleton<TokenCredential>(azureCredential);
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddApplicationInsightsTelemetry();

builder.Services.AddAzureClients(clientBuilder =>
{
    var accountEndpoint = GetRequiredConfigurationValue("CosmosDb:AccountEndpoint");
    var accountKey = GetOptionalConfigurationValue("CosmosDb:AccountKey");
    var allowInvalidServerCertificate = builder.Configuration.GetValue<bool>("CosmosDb:AllowInvalidServerCertificate");

    clientBuilder.AddClient<CosmosClient, CosmosClientOptions>((options, credential, _) =>
    {
        options.ConnectionMode = ConnectionMode.Gateway;

        if (allowInvalidServerCertificate)
        {
            options.HttpClientFactory = () =>
            {
                var handler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback =
                        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                };

                return new HttpClient(handler, disposeHandler: true);
            };
        }

        return !string.IsNullOrWhiteSpace(accountKey)
            ? new CosmosClient(accountEndpoint, accountKey, options)
            : new CosmosClient(accountEndpoint, credential, options);
    });

    clientBuilder.UseCredential(azureCredential);
});

builder.Services.AddScoped<IBatchProgressRepository, CosmosDbBatchProgressRepository>();
builder.Services.AddHostedService<EventHubConsumerService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler(exceptionHandlerApp =>
{
    exceptionHandlerApp.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(new { error = "An unexpected error occurred." });
    });
});

app.MapControllers();

if (builder.Configuration.GetValue<bool>("CosmosDb:CreateDatabaseAndContainer"))
{
    await InitializeCosmosDbAsync(app.Services, builder.Configuration);
}

app.Run();

string GetRequiredConfigurationValue(string key)
{
    var value = builder.Configuration[key];

    return !string.IsNullOrWhiteSpace(value)
        ? value
        : throw new InvalidOperationException($"{key} configuration is required.");
}

string? GetOptionalConfigurationValue(string key)
{
    var value = builder.Configuration[key];

    return !string.IsNullOrWhiteSpace(value) ? value : null;
}

static async Task InitializeCosmosDbAsync(IServiceProvider services, IConfiguration configuration)
{
    using var scope = services.CreateScope();
    var cosmosClient = scope.ServiceProvider.GetRequiredService<CosmosClient>();
    var databaseName = GetRequired(configuration, "CosmosDb:DatabaseName");
    var containerName = GetRequired(configuration, "CosmosDb:ContainerName");

    var database = await cosmosClient.CreateDatabaseIfNotExistsAsync(databaseName);
    if (database?.Database is null)
    {
        return;
    }

    await database.Database.CreateContainerIfNotExistsAsync(containerName, "/jobId");

    static string GetRequired(IConfiguration configuration, string key)
    {
        var value = configuration[key];

        return !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidOperationException($"{key} configuration is required.");
    }
}

public partial class Program;
