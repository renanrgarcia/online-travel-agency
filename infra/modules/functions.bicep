@description('Name of the storage account Durable Task and the Functions runtime both require. Storage account names must be lowercase alphanumeric, 3-24 characters, globally unique.')
param storageAccountName string

@description('Name of the Consumption plan (Y1) backing the Function App.')
param functionsPlanName string

@description('Name of the Function App -- must be globally unique across Azure, becomes <name>.azurewebsites.net.')
param functionAppName string

@description('Azure region for all resources in this module.')
param location string = resourceGroup().location

@description('Origins allowed to call this Function App cross-origin -- the frontend, once deployed (infra task 02). Empty means no browser origin is allowed; same-origin and curl are unaffected either way.')
param allowedOrigins array = []

@description('Shared HMAC key backend task 21 uses to sign (API) and verify (this Function App) price assertions -- both sides must receive the exact same value, which is why main.bicep threads one parameter into both modules rather than each generating its own. No default: FlightAi.Booking.Functions/Program.cs throws at startup if this is missing, by design, rather than silently accepting an unsigned or forged price.')
@secure()
param priceAssertionSigningKey string

// Required by both Durable Task (orchestration history, queues) and the Functions runtime itself
// (AzureWebJobsStorage) -- created here, not referenced as pre-existing, so this deployment stays
// self-contained (the same rule backend task 14 established: nothing needs to pre-exist).
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
  }
}

// Y1/Dynamic is the Consumption plan SKU -- serverless, billed per execution against the free monthly
// grant (1M executions, 400,000 GB-seconds) rather than per reserved instance, per docs/deployment.md's
// topology.
resource functionsPlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: functionsPlanName
  location: location
  sku: {
    name: 'Y1'
    tier: 'Dynamic'
  }
  properties: {
    reserved: false
  }
}

resource functionApp 'Microsoft.Web/sites@2023-12-01' = {
  name: functionAppName
  location: location
  kind: 'functionapp'
  properties: {
    serverFarmId: functionsPlan.id
    siteConfig: {
      cors: {
        allowedOrigins: allowedOrigins
      }
      appSettings: [
        {
          name: 'AzureWebJobsStorage'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}'
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet-isolated'
        }
        {
          name: 'PriceAssertion__SigningKey'
          value: priceAssertionSigningKey
        }
      ]
    }
  }
}

output functionAppDefaultHostName string = functionApp.properties.defaultHostName
output functionAppName string = functionApp.name
output storageAccountName string = storageAccount.name
