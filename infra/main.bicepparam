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

// A real secret has no business in a file committed to git -- .bicepparam files can't be layered with
// an inline CLI --parameters override the way classic parameters.json can (confirmed empirically: BCP258
// on every non-defaulted param not assigned inside this file), so readEnvironmentVariable is the
// supported way to keep the value itself out of source control while still using this file. Set
// PRICE_ASSERTION_SIGNING_KEY in the shell before deploying; see infra/README.md.
param priceAssertionSigningKey = readEnvironmentVariable('PRICE_ASSERTION_SIGNING_KEY')

// Unlike PRICE_ASSERTION_SIGNING_KEY above, this one has a fallback ('') as its second argument --
// deploying with no Gemini key set is a supported state (Program.cs falls back to the offline chat
// client), so an unset GEMINI_API_KEY shouldn't fail the deployment the way an unset signing key does.
param geminiApiKey = readEnvironmentVariable('GEMINI_API_KEY', '')
