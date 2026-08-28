@description('Name of the App Service Plan.')
param appServicePlanName string

@description('Name of the Web App -- must be globally unique across Azure, becomes <name>.azurewebsites.net.')
param webAppName string

@description('Azure region for all resources in this module.')
param location string = resourceGroup().location

@description('.NET runtime version string for the Windows site config. Verify against `az webapp list-runtimes --os windows` before deploying -- .NET 10 is new enough that this exact string is not yet confirmed against a live subscription.')
param netFrameworkVersion string = 'v10.0'

// F1 (Free) and D1 (Shared) tiers only exist on Windows App Service plans -- there is no free tier on
// Linux. `reserved: false` is what selects Windows (Linux plans set this true).
resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: appServicePlanName
  location: location
  sku: {
    name: 'F1'
    tier: 'Free'
  }
  properties: {
    reserved: false
  }
}

resource webApp 'Microsoft.Web/sites@2023-12-01' = {
  name: webAppName
  location: location
  properties: {
    serverFarmId: appServicePlan.id
    siteConfig: {
      netFrameworkVersion: netFrameworkVersion
      // F1 does not support Always On -- deployment fails if this is true on this SKU.
      alwaysOn: false
    }
  }
}

output webAppDefaultHostName string = webApp.properties.defaultHostName
output webAppName string = webApp.name
