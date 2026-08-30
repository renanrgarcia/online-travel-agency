targetScope = 'subscription'

@description('Short environment name, used to build default resource names (e.g. "dev").')
param environmentName string = 'dev'

@description('Azure region for every resource this deployment creates, including the resource group itself.')
param location string = 'brazilsouth'

@description('Resource group name. Created by this deployment if it does not already exist -- nothing needs to pre-exist.')
param resourceGroupName string = 'rg-flightai-${environmentName}'

@description('App Service Plan name.')
param appServicePlanName string = 'flightai-plan-${environmentName}'

@description('Web App name -- must be globally unique across all of Azure, becomes <name>.azurewebsites.net. Override this directly in main.bicepparam if the default collides with an existing name.')
param webAppName string = 'flightai-api-${environmentName}'

@description('Consumption plan name for the Booking Functions app.')
param functionsPlanName string = 'flightai-funcsplan-${environmentName}'

@description('Function App name -- must be globally unique across all of Azure, becomes <name>.azurewebsites.net. Override this directly in main.bicepparam if the default collides with an existing name.')
param functionAppName string = 'flightai-booking-${environmentName}'

@description('Storage account name for Durable Task state -- lowercase alphanumeric only (no hyphens), 3-24 characters, globally unique. Override in main.bicepparam if the default collides.')
param storageAccountName string = 'flightaifuncs${environmentName}'

resource rg 'Microsoft.Resources/resourceGroups@2024-11-01' = {
  name: resourceGroupName
  location: location
}

module appService 'modules/app-service.bicep' = {
  name: 'appServiceDeployment'
  scope: rg
  params: {
    appServicePlanName: appServicePlanName
    webAppName: webAppName
    location: location
  }
}

module functionsApp 'modules/functions.bicep' = {
  name: 'functionsDeployment'
  scope: rg
  params: {
    storageAccountName: storageAccountName
    functionsPlanName: functionsPlanName
    functionAppName: functionAppName
    location: location
  }
}

output resourceGroupName string = rg.name
output webAppUrl string = 'https://${appService.outputs.webAppDefaultHostName}'
output webAppName string = appService.outputs.webAppName
output functionAppUrl string = 'https://${functionsApp.outputs.functionAppDefaultHostName}'
output functionAppName string = functionsApp.outputs.functionAppName
