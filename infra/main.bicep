targetScope = 'resourceGroup'

@description('Short environment name, used to build resource names (e.g. "dev").')
param environmentName string = 'dev'

@description('Azure region for all resources.')
param location string = resourceGroup().location

module appService 'modules/app-service.bicep' = {
  name: 'appServiceDeployment'
  params: {
    appServicePlanName: 'flightai-plan-${environmentName}'
    webAppName: 'flightai-api-${environmentName}'
    location: location
  }
}

output webAppUrl string = 'https://${appService.outputs.webAppDefaultHostName}'
output webAppName string = appService.outputs.webAppName
