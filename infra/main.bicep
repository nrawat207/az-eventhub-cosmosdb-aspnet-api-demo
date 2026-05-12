param environment string = 'dev'
param location string = resourceGroup().location
param prefix string = 'azlearn'
param batchProcessorImage string = '${toLower(prefix)}acr${environment}.azurecr.io/batchprocessor-api:dev'
param progressReceiverImage string = '${toLower(prefix)}acr${environment}.azurecr.io/progressreceiver-api:dev'
param cpuCores string = '0.5'
param memoryGi string = '1Gi'
param minReplicas int = 1
param maxReplicas int = 3

var batchProcessorAppName = '${prefix}-batchprocessor-${environment}'
var progressReceiverAppName = '${prefix}-progressreceiver-${environment}'
var databaseName = 'BatchJobsDB'
var containerName = 'BatchJobs'

resource batchProcessorIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${prefix}-id-batchprocessor-${environment}'
  location: location
}

resource progressReceiverIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${prefix}-id-progressreceiver-${environment}'
  location: location
}

module network 'modules/network.bicep' = {
  name: 'network'
  params: {
    location: location
    environment: environment
    prefix: prefix
  }
}

module monitoring 'modules/monitoring.bicep' = {
  name: 'monitoring'
  params: {
    location: location
    environment: environment
    prefix: prefix
  }
}

module acaEnvironment 'modules/aca-environment.bicep' = {
  name: 'aca-environment'
  params: {
    location: location
    environment: environment
    prefix: prefix
    acaSubnetId: network.outputs.acaSubnetId
    logAnalyticsWorkspaceId: monitoring.outputs.workspaceId
  }
}

module containerRegistry 'modules/containerregistry.bicep' = {
  name: 'container-registry'
  params: {
    location: location
    environment: environment
    prefix: prefix
    principalIds: [
      batchProcessorIdentity.properties.principalId
      progressReceiverIdentity.properties.principalId
    ]
  }
}

module keyVault 'modules/keyvault.bicep' = {
  name: 'key-vault'
  params: {
    location: location
    environment: environment
    prefix: prefix
    vnetId: network.outputs.vnetId
    privateEndpointsSubnetId: network.outputs.privateEndpointsSubnetId
    principalIds: [
      batchProcessorIdentity.properties.principalId
      progressReceiverIdentity.properties.principalId
    ]
  }
}

module eventHub 'modules/eventhub.bicep' = {
  name: 'event-hub'
  params: {
    location: location
    environment: environment
    prefix: prefix
    vnetId: network.outputs.vnetId
    privateEndpointsSubnetId: network.outputs.privateEndpointsSubnetId
    batchProcessorPrincipalId: batchProcessorIdentity.properties.principalId
    progressReceiverPrincipalId: progressReceiverIdentity.properties.principalId
  }
}

module cosmosDb 'modules/cosmosdb.bicep' = {
  name: 'cosmos-db'
  params: {
    location: location
    environment: environment
    prefix: prefix
    vnetId: network.outputs.vnetId
    privateEndpointsSubnetId: network.outputs.privateEndpointsSubnetId
    progressReceiverPrincipalId: progressReceiverIdentity.properties.principalId
    databaseName: databaseName
    containerName: containerName
  }
}

module storage 'modules/storage.bicep' = {
  name: 'storage'
  params: {
    location: location
    environment: environment
    prefix: prefix
    vnetId: network.outputs.vnetId
    privateEndpointsSubnetId: network.outputs.privateEndpointsSubnetId
    progressReceiverPrincipalId: progressReceiverIdentity.properties.principalId
  }
}

var commonEnvVars = [
  {
    name: 'ASPNETCORE_ENVIRONMENT'
    value: 'Production'
  }
  {
    name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
    value: monitoring.outputs.appInsightsConnectionString
  }
  {
    name: 'AZURE_KEY_VAULT_URI'
    value: keyVault.outputs.keyVaultUri
  }
  {
    name: 'EventHub__Name'
    value: eventHub.outputs.eventHubName
  }
  {
    name: 'EventHub__NamespaceFQDN'
    value: eventHub.outputs.namespaceFqdn
  }
]

module batchProcessorApp 'modules/containerapp.bicep' = {
  name: 'batch-processor-container-app'
  params: {
    location: location
    appName: batchProcessorAppName
    image: batchProcessorImage
    environmentId: acaEnvironment.outputs.environmentId
    identityId: batchProcessorIdentity.id
    registryServer: containerRegistry.outputs.loginServer
    envVars: commonEnvVars
    cpuCores: cpuCores
    memoryGi: memoryGi
    minReplicas: minReplicas
    maxReplicas: maxReplicas
  }
}

module progressReceiverApp 'modules/containerapp.bicep' = {
  name: 'progress-receiver-container-app'
  params: {
    location: location
    appName: progressReceiverAppName
    image: progressReceiverImage
    environmentId: acaEnvironment.outputs.environmentId
    identityId: progressReceiverIdentity.id
    registryServer: containerRegistry.outputs.loginServer
    envVars: concat(commonEnvVars, [
      {
        name: 'CosmosDb__AccountEndpoint'
        value: cosmosDb.outputs.accountEndpoint
      }
      {
        name: 'CosmosDb__DatabaseName'
        value: databaseName
      }
      {
        name: 'CosmosDb__ContainerName'
        value: containerName
      }
      {
        name: 'Storage__BlobServiceUri'
        value: storage.outputs.blobServiceUri
      }
      {
        name: 'Storage__CheckpointContainerUri'
        value: storage.outputs.checkpointContainerUri
      }
    ])
    cpuCores: cpuCores
    memoryGi: memoryGi
    minReplicas: minReplicas
    maxReplicas: maxReplicas
  }
}

output containerRegistryName string = containerRegistry.outputs.registryName
output containerRegistryLoginServer string = containerRegistry.outputs.loginServer
output batchProcessorIdentityId string = batchProcessorIdentity.id
output progressReceiverIdentityId string = progressReceiverIdentity.id
output batchProcessorContainerAppId string = batchProcessorApp.outputs.containerAppId
output progressReceiverContainerAppId string = progressReceiverApp.outputs.containerAppId
output keyVaultUri string = keyVault.outputs.keyVaultUri
output eventHubNamespaceFqdn string = eventHub.outputs.namespaceFqdn
output cosmosDbEndpoint string = cosmosDb.outputs.accountEndpoint
output checkpointContainerUri string = storage.outputs.checkpointContainerUri
