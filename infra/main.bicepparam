using 'main.bicep'

param environmentName = 'dev'

// brazilsouth is the eventual target (this is a Brazilian travel company's app) but this subscription's
// F1 (Free tier) quota there is 0 by default and needs a support-ticket-approved increase -- confirmed
// via `az quota list` against Microsoft.Web/locations/brazilsouth, not guessed. Same restriction hit
// eastus, eastus2, northeurope, and uksouth. westeurope, centralus, and westus2 all validated clean
// (no quota error, confirmed via `az deployment sub what-if` against each) -- use one of those for now,
// switch back to brazilsouth once the quota request clears.
param location = 'westeurope'

// Uncomment and change if the default 'flightai-api-dev' is already taken -- Web App names must be
// globally unique across all of Azure, not just this subscription:
// param webAppName = 'flightai-api-<something-more-unique>'
