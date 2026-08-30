@description('Name of the Static Web App.')
param staticWebAppName string

@description('Azure region for the Static Web App. Free tier region availability is more limited than App Service/Functions -- verify with `az staticwebapp list-environments` or a what-if before assuming a given region works.')
param location string

// No repositoryUrl/branch/buildProperties -- deliberately not using Static Web Apps' own built-in
// GitHub integration (which would generate and own a second, separate workflow file). Deployed instead
// via the existing ci-cd.yml, authenticated with this resource's own deployment token.
resource staticWebApp 'Microsoft.Web/staticSites@2023-12-01' = {
  name: staticWebAppName
  location: location
  sku: {
    name: 'Free'
    tier: 'Free'
  }
  properties: {}
}

output staticWebAppDefaultHostname string = staticWebApp.properties.defaultHostname
output staticWebAppName string = staticWebApp.name
