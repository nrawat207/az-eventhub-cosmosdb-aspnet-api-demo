using Azure.Identity;
using BatchProcessor.Api.Services;
using Microsoft.Extensions.Azure;

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

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddApplicationInsightsTelemetry();

builder.Services.AddAzureClients(clientBuilder =>
{
    var eventHubName = GetRequiredConfigurationValue("EventHub:Name");

    var connectionString = GetOptionalConfigurationValue("ConnectionStrings:EventHub")
        ?? GetOptionalConfigurationValue("EventHub:ConnectionString");

    if (!string.IsNullOrWhiteSpace(connectionString))
    {
        clientBuilder.AddEventHubProducerClient(connectionString, eventHubName);
    }
    else
    {
        var fullyQualifiedNamespace = GetRequiredConfigurationValue("EventHub:NamespaceFQDN");

        clientBuilder.AddEventHubProducerClientWithNamespace(fullyQualifiedNamespace, eventHubName);
        clientBuilder.UseCredential(azureCredential);
    }
});

builder.Services.AddSingleton<EventHubPublisherService>();
builder.Services.AddSingleton<BatchJobService>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<BatchJobService>());
builder.Services.AddScoped<IBatchJobService>(provider => provider.GetRequiredService<BatchJobService>());

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

app.MapHealthChecks("/health");
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
