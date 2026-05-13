#
# setup-keyvault-secrets.ps1
#
# Sets up Azure Key Vault secrets required by the application.
# This script accepts parameters and populates Key Vault with connection strings
# and endpoints for CosmosDB, EventHub, and Application Insights.
#
# Usage:
#   .\setup-keyvault-secrets.ps1 `
#     -ResourceGroup <RG_NAME> `
#     -KeyVaultName <KV_NAME> `
#     -CosmosEndpoint <COSMOS_ENDPOINT> `
#     -EventHubFqdn <EVENTHUB_FQDN> `
#     -AppInsightsConnectionString <APPINSIGHTS_CONNSTR>
#

param(
    [Parameter(Mandatory = $true)]
    [string]$ResourceGroup,

    [Parameter(Mandatory = $true)]
    [string]$KeyVaultName,

    [Parameter(Mandatory = $true)]
    [string]$CosmosEndpoint,

    [Parameter(Mandatory = $true)]
    [string]$EventHubFqdn,

    [Parameter(Mandatory = $true)]
    [string]$AppInsightsConnectionString
)

$ErrorActionPreference = "Stop"

# Function to print colored output
function Write-Success {
    param([string]$Message)
    Write-Host "✓ $Message" -ForegroundColor Green
}

function Write-Error-Custom {
    param([string]$Message)
    Write-Host "✗ $Message" -ForegroundColor Red
}

function Write-Info {
    param([string]$Message)
    Write-Host "ℹ $Message" -ForegroundColor Yellow
}

Write-Info "Setting up Key Vault secrets..."
Write-Info "Resource Group: $ResourceGroup"
Write-Info "Key Vault: $KeyVaultName"
Write-Host ""

$secretsSet = 0
$secretsFailed = 0

# Secret 1: CosmosDB Account Endpoint
# Purpose: Used by ProgressReceiver.Api to connect to CosmosDB for reading/writing batch job status
Write-Info "Setting secret: CosmosDb--AccountEndpoint"
try {
    az keyvault secret set `
        --name "CosmosDb--AccountEndpoint" `
        --value $CosmosEndpoint `
        --vault-name $KeyVaultName `
        --resource-group $ResourceGroup `
        --output none
    Write-Success "CosmosDb--AccountEndpoint"
    $secretsSet++
}
catch {
    Write-Error-Custom "Failed to set CosmosDb--AccountEndpoint"
    Write-Error-Custom $_.Exception.Message
    $secretsFailed++
}

# Secret 2: EventHub Namespace FQDN
# Purpose: Used by BatchProcessor.Api to connect to EventHub for publishing batch progress events
Write-Info "Setting secret: EventHub--NamespaceFQDN"
try {
    az keyvault secret set `
        --name "EventHub--NamespaceFQDN" `
        --value $EventHubFqdn `
        --vault-name $KeyVaultName `
        --resource-group $ResourceGroup `
        --output none
    Write-Success "EventHub--NamespaceFQDN"
    $secretsSet++
}
catch {
    Write-Error-Custom "Failed to set EventHub--NamespaceFQDN"
    Write-Error-Custom $_.Exception.Message
    $secretsFailed++
}

# Secret 3: Application Insights Connection String
# Purpose: Used by both APIs for instrumenting telemetry and application insights tracking
Write-Info "Setting secret: ApplicationInsights--ConnectionString"
try {
    az keyvault secret set `
        --name "ApplicationInsights--ConnectionString" `
        --value $AppInsightsConnectionString `
        --vault-name $KeyVaultName `
        --resource-group $ResourceGroup `
        --output none
    Write-Success "ApplicationInsights--ConnectionString"
    $secretsSet++
}
catch {
    Write-Error-Custom "Failed to set ApplicationInsights--ConnectionString"
    Write-Error-Custom $_.Exception.Message
    $secretsFailed++
}

Write-Host ""
Write-Info "Secret setup summary:"
Write-Success "$secretsSet secret(s) successfully set"
if ($secretsFailed -gt 0) {
    Write-Error-Custom "$secretsFailed secret(s) failed"
    exit 1
}

Write-Success "All secrets have been successfully configured in Key Vault!"
