@description('Name of the App Service Plan.')
param appServicePlanName string

@description('Name of the Web App -- must be globally unique across Azure, becomes <name>.azurewebsites.net.')
param webAppName string

@description('Azure region for all resources in this module.')
param location string = resourceGroup().location

@description('.NET runtime version string for the Windows site config. Verify against `az webapp list-runtimes --os windows` before deploying -- .NET 10 is new enough that this exact string is not yet confirmed against a live subscription.')
param netFrameworkVersion string = 'v10.0'

@description('Origins allowed to call this API cross-origin -- the frontend, once deployed (infra task 02). Empty means no browser origin is allowed; same-origin and curl are unaffected either way. Sets Cors__AllowedOrigins__N app settings, which Program.cs reads via ASP.NET Core CORS middleware -- not the Azure platform CORS feature (see functions.bicep for that variant).')
param allowedOrigins array = []

@description('Shared HMAC key backend task 21 uses to sign (this API) and verify (Booking Functions) price assertions -- both sides must receive the exact same value, which is why main.bicep threads one parameter into both modules rather than each generating its own. No default: Program.cs throws at startup if this is missing, by design, rather than silently accepting an unsigned or forged price.')
@secure()
param priceAssertionSigningKey string

@description('Gemini API key (task 17) -- read by Program.cs to decide whether to build a real Gemini IChatClient or fall back to the deterministic offline one. Only FlightAi.Api ever calls a model, so this is not threaded into functions.bicep. Empty is a safe default, unlike priceAssertionSigningKey: Program.cs already treats a missing key as "use the offline client," not a startup failure.')
@secure()
param geminiApiKey string = ''

@description('Duffel test-mode API token (backend task 25) -- read by Program.cs to decide whether to register a real DuffelConnector alongside the existing mock suppliers. Only FlightAi.Api ever calls a supplier connector, so this is not threaded into functions.bicep. Empty default, same reasoning as geminiApiKey: a missing key means "no DuffelConnector registered," not a startup failure.')
@secure()
param duffelApiKey string = ''

// A for-expression can only be the direct value of a resource/module/variable/output -- not nested
// inside a function call like concat(...) -- so the CORS entries are built here first, then combined
// with the static signing-key setting below.
var corsAppSettings = [
  for (origin, i) in allowedOrigins: {
    name: 'Cors__AllowedOrigins__${i}'
    value: origin
  }
]

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
      appSettings: concat(
        [
          {
            name: 'PriceAssertion__SigningKey'
            value: priceAssertionSigningKey
          }
          {
            name: 'Gemini__ApiKey'
            value: geminiApiKey
          }
          {
            name: 'Duffel__ApiKey'
            value: duffelApiKey
          }
        ],
        corsAppSettings
      )
    }
  }
}

// Filesystem app logging (free, capped, self-rotating) rather than Application Insights (task 23):
// this is a free-tier reference project, and a new resource plus a package dependency isn't worth it
// just to durably capture the handful of unhandled-exception logs UseExceptionHandler now produces.
// Declared here so it survives a redeploy -- previously only set by hand via `az webapp log config`,
// which does not persist through IaC.
resource webAppLogs 'Microsoft.Web/sites/config@2023-12-01' = {
  parent: webApp
  name: 'logs'
  properties: {
    applicationLogs: {
      fileSystem: {
        level: 'Warning'
      }
    }
  }
}

output webAppDefaultHostName string = webApp.properties.defaultHostName
output webAppName string = webApp.name
