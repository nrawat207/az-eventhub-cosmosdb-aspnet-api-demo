param location string
param environment string
param prefix string
param acaSubnetId string
param logAnalyticsWorkspaceId string

resource managedEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: '${prefix}-acaenv-${environment}'
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: reference(logAnalyticsWorkspaceId, '2023-09-01').customerId
        sharedKey: listKeys(logAnalyticsWorkspaceId, '2023-09-01').primarySharedKey
      }
    }
    vnetConfiguration: {
      infrastructureSubnetId: acaSubnetId
      internal: true
    }
  }
}

output environmentId string = managedEnvironment.id
output defaultDomain string = managedEnvironment.properties.defaultDomain
