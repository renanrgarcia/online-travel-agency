# Infrastructure as Code

Bicep, not Terraform or hand-written ARM JSON -- no state file to manage (Azure Resource Manager tracks
deployments itself), first-class `az`/VS Code tooling, and it's what AZ-104 actually expects you to
author today over raw ARM JSON.

Now covers the App Service, the Booking Functions app, and the Static Web App. See
[`docs/features/03-infra/README.md`](../docs/features/03-infra/README.md) for why infrastructure for
both backend and frontend lives in its own feature rather than inside either one.

## Layout

- `main.bicep` -- **subscription-scoped**: creates the resource group itself, then deploys the modules
  below into it. Nothing needs to pre-exist in the subscription.
- `main.bicepparam` -- parameter values for this deployment.
- `modules/app-service.bicep` -- **resource-group-scoped**: the App Service Plan (F1/Free, Windows) and
  the Web App itself.
- `modules/functions.bicep` -- **resource-group-scoped**: the Consumption plan (Y1), the Function App
  (`kind: 'functionapp'`, `dotnet-isolated` worker), and the Storage account Durable Task and the
  Functions runtime both require (`AzureWebJobsStorage`) -- created by the template, not referenced as
  pre-existing, same rule as everything else here.
- `modules/static-web-app.bicep` -- **resource-group-scoped**: the Static Web App (Free tier). No
  `repositoryUrl`/GitHub linkage -- deployed via `ci-cd.yml`'s own job, not Static Web Apps' built-in
  (and separate) GitHub integration.

`main.bicep` threads the Static Web App's own `defaultHostname` output directly into both backends'
`allowedOrigins` CORS parameters -- not typed in twice, not a manual copy-paste step. Bicep infers the
dependency automatically, so the App Service and Function App always deploy (or redeploy, on an
incremental update) after the Static Web App, whether or not they already exist.

## Before you deploy

- Azure CLI installed (`az --version`) and logged in (`az login`).
- Bicep CLI available (`az bicep version`; `az bicep install` if missing).
- `webAppName`, `functionAppName`, and `staticWebAppName` (see `main.bicepparam`) must each be
  **globally unique across all of Azure** -- the first two become `<name>.azurewebsites.net`, the last
  becomes `<name>.azurestaticapps.net`. The defaults are `flightai-api-dev`, `flightai-booking-dev`, and
  `flightai-web-dev`; if deployment fails on a name conflict, uncomment and change the relevant line in
  `main.bicepparam`.
- `storageAccountName` has the same global-uniqueness requirement, plus a stricter format: lowercase
  alphanumeric only, no hyphens, 3-24 characters. The default is `flightaifuncsdev`.
- `location` is currently `westeurope`, not `brazilsouth` -- see the comment in `main.bicepparam` for
  why. Short version: F1 (Free) tier is *available* in Brazil South (confirmed via
  `az appservice list-locations --sku FREE`), but this subscription's F1 *quota* there defaults to 0 and
  needs a support-ticket-approved increase before anything can actually deploy there. `westeurope`,
  `centralus`, and `westus2` all validated clean with no quota error -- switch `location` back to
  `brazilsouth` once that ticket clears.
- More generally: SKU *availability* in a region and this subscription's *quota* for that SKU in that
  region are two different checks, and Azure will let a `what-if` fail on either one. If a region you'd
  expect to work doesn't, check both -- `az appservice list-locations --sku <SKU>` for availability,
  `az quota list --scope /subscriptions/<id>/providers/Microsoft.Web/locations/<region>` for the
  App-Service-specific quota (this is a different quota system from the general Compute vCPU quota the
  portal's Quotas blade shows by default).
- `netFrameworkVersion` in `modules/app-service.bicep` defaults to `'v10.0'` but has **not** been
  verified against a live subscription -- .NET 10 is very new. Check what's actually available:

  ```bash
  az webapp list-runtimes --os windows | grep -i dotnet
  ```

  and adjust the parameter if the listed string differs.

## Secrets

Two `@secure()` parameters flow from environment variables into `main.bicepparam` via
`readEnvironmentVariable(...)`, never as literal values in a file committed to git -- `.bicepparam`
files can't be layered with an inline CLI `--parameters` override the way classic `parameters.json`
files can (confirmed empirically: `BCP258` on every non-defaulted param not assigned inside the file),
so this is the supported way to keep a real secret out of source control while still using the file.

- `PRICE_ASSERTION_SIGNING_KEY` -- required, no fallback. `FlightAi.Api` and
  `FlightAi.Booking.Functions` both throw at startup if this is missing (backend task 21) -- deploying
  without it set fails loudly rather than silently shipping two apps that crash-loop in production, which
  is exactly what happened the first time this landed without the app setting wired up.
- `GEMINI_API_KEY` -- optional, defaults to an empty string. Backend task 17: present means
  `FlightAi.Api` builds a real Gemini-backed `IChatClient`; absent means it falls back to the
  deterministic offline client, same as local dev with no key set. Only `FlightAi.Api` ever calls a
  model, so this is threaded into `modules/app-service.bicep` alone, not `modules/functions.bicep`.

Set whichever you need before `az bicep build-params` / `what-if` / `create`:

```bash
export PRICE_ASSERTION_SIGNING_KEY="$(openssl rand -base64 32)"   # must match what's already live -- see below
export GEMINI_API_KEY="<your key>"                                 # omit entirely to deploy without a real model
```

`PRICE_ASSERTION_SIGNING_KEY` in particular must stay **stable** across redeploys -- regenerating it
invalidates any price assertion currently in flight between a search response and a booking request.
Keep it in a local, gitignored `infra/.env` (already in `.gitignore`) and `source` that file rather than
generating a fresh value each time.

## Deploy

Both files were validated locally with `az bicep build` / `az bicep build-params` (catches syntax and
schema errors -- wrong resource types, wrong API versions, type mismatches -- without needing a live
subscription). That's necessary but not sufficient; `what-if` is the real pre-flight check, since it
asks Azure itself what would actually happen:

```bash
az deployment sub what-if \
  --name flightai-dev \
  --location westeurope \
  --template-file main.bicep \
  --parameters main.bicepparam
```

Then apply -- subscription-scoped deployments use `az deployment sub create`, not
`az deployment group create` (there's no group to target yet; this command creates it):

```bash
az deployment sub create \
  --name flightai-dev \
  --location westeurope \
  --template-file main.bicep \
  --parameters main.bicepparam
```

Always pass `--name` explicitly. A subscription-level deployment is itself a tracked object (visible via
`az deployment sub list`, and in the Portal under the subscription's "Deployments" blade) -- without
`--name`, Azure defaults it to the template's filename (`main.bicep` -> deployment named "main"), and
that name is permanently pinned to whichever location it was first used with. Reusing the same name for
a different location fails with `InvalidDeploymentLocation`, even if the original deployment itself
failed and created nothing -- which is exactly what happened here (an early attempt at `brazilsouth`
failed on the F1 quota issue, but the deployment record "main" still exists, pinned there).

The `--location` flag here is the deployment *operation's* tracking location (a subscription-level
deployment needs one), separate from the `location` parameter inside `main.bicepparam` that decides
where the actual resources land -- keep them in sync, since a mismatch doesn't error, it just tracks the
deployment somewhere different from where the resources actually are.

The deployment outputs `webAppUrl` and `functionAppUrl` -- where the zip-deployed API and the
Functions app will actually be reachable.

One thing worth knowing before your first `what-if` after this module was added: it may show a small
`Modify` on the *already-deployed* `flightai-api-dev` resource (`netFrameworkVersion`,
`localMySqlEnabled`) even though `modules/app-service.bicep` itself hasn't changed. `what-if`'s own
output warns it "may contain false positive predictions" for exactly this kind of nested `siteConfig`
diff -- a real `deployment sub create` is the actual test of idempotence (task 01's E3 in
`docs/features/03-infra/`), not `what-if` alone.
