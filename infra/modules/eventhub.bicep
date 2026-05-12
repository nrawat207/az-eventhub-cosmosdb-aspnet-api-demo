param location string
param environment string
param prefix string
param vnetId string
param privateEndpointsSubnetId string
param batchProcessorPrincipalId string
param progressReceiverPrincipalId string

var eventHubsDataSenderRoleDefinitionId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '2b629674-e913-4c01-ae53-ef4638d8f975')
var eventHubsDataReceiverRoleDefinitionId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'a638d3c7-ab3a-418d-83e6-5f17a39d4fde')

resource namespace 'Microsoft.EventHub/namespaces@2024-01-01' = {
  name: '${prefix}-evhns-${environment}'
  location: location
  sku: {
    name: 'Standard'
    tier: 'Standard'
    capacity: 1
  }
  properties: {
    publicNetworkAccess: 'Disabled'
    disableLocalAuth: true
    minimumTlsVersion: '1.2'
  }
}

resource networkRuleSet 'Microsoft.EventHub/namespaces/networkRuleSets@2024-01-01' = {
  parent: namespace
  name: 'default'
  properties: {
    defaultAction: 'Deny'
    publicNetworkAccess: 'Disabled'
    trustedServiceAccessEnabled: true
  }
}

resource eventHub 'Microsoft.EventHub/namespaces/eventhubs@2024-01-01' = {
  parent: namespace
  name: 'batch-progress'
  properties: {
    partitionCount: 4
    messageRetentionInDays: 1
  }
}

resource defaultConsumerGroup 'Microsoft.EventHub/namespaces/eventhubs/consumergroups@2024-01-01' = {
  parent: eventHub
  name: '$Default'
}

resource privateDnsZone 'Microsoft.Network/privateDnsZones@2020-06-01' = {
  name: 'privatelink.servicebus.windows.net'
  location: 'global'
}

resource privateDnsZoneLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2020-06-01' = {
  parent: privateDnsZone
  name: '${prefix}-evh-dnslink-${environment}'
  location: 'global'
  properties: {
    registrationEnabled: false
    virtualNetwork: {
      id: vnetId
    }
  }
}

resource privateEndpoint 'Microsoft.Network/privateEndpoints@2024-05-01' = {
  name: '${prefix}-evh-pe-${environment}'
  location: location
  properties: {
    subnet: {
      id: privateEndpointsSubnetId
    }
    privateLinkServiceConnections: [
      {
        name: '${prefix}-evh-pls-${environment}'
        properties: {
          privateLinkServiceId: namespace.id
          groupIds: [
            'namespace'
          ]
        }
      }
    ]
  }
}

resource privateDnsZoneGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2024-05-01' = {
  parent: privateEndpoint
  name: 'default'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'privatelink-servicebus-windows-net'
        properties: {
          privateDnsZoneId: privateDnsZone.id
        }
      }
    ]
  }
}

resource senderAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(namespace.id, batchProcessorPrincipalId, eventHubsDataSenderRoleDefinitionId)
  scope: namespace
  properties: {
    roleDefinitionId: eventHubsDataSenderRoleDefinitionId
    principalId: batchProcessorPrincipalId
    principalType: 'ServicePrincipal'
  }
}

resource receiverAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(namespace.id, progressReceiverPrincipalId, eventHubsDataReceiverRoleDefinitionId)
  scope: namespace
  properties: {
    roleDefinitionId: eventHubsDataReceiverRoleDefinitionId
    principalId: progressReceiverPrincipalId
    principalType: 'ServicePrincipal'
  }
}

output namespaceId string = namespace.id
output namespaceName string = namespace.name
output namespaceFqdn string = '${namespace.name}.servicebus.windows.net'
output eventHubName string = eventHub.name
