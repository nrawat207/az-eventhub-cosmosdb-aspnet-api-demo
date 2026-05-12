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

    clientBuilder.AddClient<CosmosClient, CosmosClientOptions>((options, credential, _) =>
    {
        options.ConnectionMode = ConnectionMode.Gateway;

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

public partial class Program;
