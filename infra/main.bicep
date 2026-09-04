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

@description('Static Web App name -- must be globally unique across all of Azure, becomes <name>.azurestaticapps.net. Override in main.bicepparam if the default collides.')
param staticWebAppName string = 'flightai-web-${environmentName}'

@description('Durable Task Scheduler name -- must be unique within the resource group.')
param durableTaskSchedulerName string = 'flightai-dts-${environmentName}'

@description('Durable Task Scheduler task hub name.')
param durableTaskSchedulerTaskHubName string = 'default'

@description('User-assigned managed identity name for the Durable Task Scheduler connection.')
param durableTaskSchedulerIdentityName string = 'flightai-booking-identity-${environmentName}'

@description('Shared HMAC key backend task 21 uses to sign (API) and verify (Booking Functions) price assertions. No default on purpose -- a real secret has no business living in main.bicepparam, which is committed to git. Supply it at deploy time only: --parameters priceAssertionSigningKey=<value>.')
@secure()
param priceAssertionSigningKey string

@description('Gemini API key (task 17) -- only FlightAi.Api reads it, so it is threaded into the appService module alone, not functionsApp. Empty default: unlike priceAssertionSigningKey, deploying with no key set is a supported state -- Program.cs falls back to the offline chat client rather than failing.')
@secure()
param geminiApiKey string = ''

resource rg 'Microsoft.Resources/resourceGroups@2024-11-01' = {
  name: resourceGroupName
  location: location
}

module staticWebApp 'modules/static-web-app.bicep' = {
  name: 'staticWebAppDeployment'
  scope: rg
  params: {
    staticWebAppName: staticWebAppName
    location: location
  }
}

module durableTaskScheduler 'modules/durable-task-scheduler.bicep' = {
  name: 'durableTaskSchedulerDeployment'
  scope: rg
  params: {
    schedulerName: durableTaskSchedulerName
    taskHubName: durableTaskSchedulerTaskHubName
    identityName: durableTaskSchedulerIdentityName
    location: location
  }
}

// Both backends allow exactly the frontend's own hostname, resolved from the SWA module's own output --
// not typed in twice, not a manual copy-paste once the Static Web App exists. Bicep infers the
// dependency automatically: appService/functionsApp deploy after staticWebApp, every time, fresh
// deployment or incremental update alike.
var frontendOrigin = 'https://${staticWebApp.outputs.staticWebAppDefaultHostname}'

module appService 'modules/app-service.bicep' = {
  name: 'appServiceDeployment'
  scope: rg
  params: {
    appServicePlanName: appServicePlanName
    webAppName: webAppName
    location: location
    allowedOrigins: [frontendOrigin]
    priceAssertionSigningKey: priceAssertionSigningKey
    geminiApiKey: geminiApiKey
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
    allowedOrigins: [frontendOrigin]
    priceAssertionSigningKey: priceAssertionSigningKey
    durableTaskSchedulerEndpoint: durableTaskScheduler.outputs.schedulerEndpoint
    durableTaskSchedulerTaskHubName: durableTaskScheduler.outputs.taskHubName
    durableTaskSchedulerIdentityResourceId: durableTaskScheduler.outputs.identityResourceId
    durableTaskSchedulerIdentityClientId: durableTaskScheduler.outputs.identityClientId
  }
}

output resourceGroupName string = rg.name
output webAppUrl string = 'https://${appService.outputs.webAppDefaultHostName}'
output webAppName string = appService.outputs.webAppName
output functionAppUrl string = 'https://${functionsApp.outputs.functionAppDefaultHostName}'
output functionAppName string = functionsApp.outputs.functionAppName
output staticWebAppUrl string = frontendOrigin
output staticWebAppName string = staticWebApp.outputs.staticWebAppName
