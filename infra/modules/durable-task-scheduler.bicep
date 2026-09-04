@description('Name of the Durable Task Scheduler resource.')
param schedulerName string

@description('Name of the task hub used by the Booking Functions app.')
param taskHubName string = 'default'

@description('Name of the user-assigned identity used by the Booking Functions app to access the scheduler.')
param identityName string

@description('Azure region for the scheduler and identity.')
param location string = resourceGroup().location

var durableTaskDataContributorRoleDefinitionId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  '0ad04412-c4d5-4796-b79c-f76d14c8d402')

resource scheduler 'Microsoft.DurableTask/schedulers@2026-02-01' = {
  name: schedulerName
  location: location
  properties: {
    ipAllowlist: [
      '0.0.0.0/0'
    ]
    publicNetworkAccess: 'Enabled'
    sku: {
      name: 'Consumption'
    }
  }
}

resource taskHub 'Microsoft.DurableTask/schedulers/taskHubs@2026-02-01' = {
  parent: scheduler
  name: taskHubName
  properties: {}
}

resource bookingIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: identityName
  location: location
}

resource schedulerRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(taskHub.id, identityName, durableTaskDataContributorRoleDefinitionId)
  scope: taskHub
  properties: {
    principalId: bookingIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: durableTaskDataContributorRoleDefinitionId
  }
}

output schedulerEndpoint string = scheduler.properties.endpoint
output taskHubName string = taskHub.name
output identityResourceId string = bookingIdentity.id
output identityClientId string = bookingIdentity.properties.clientId
