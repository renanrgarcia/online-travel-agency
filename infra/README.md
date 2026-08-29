# Infrastructure as Code

Bicep, not Terraform or hand-written ARM JSON -- no state file to manage (Azure Resource Manager tracks
deployments itself), first-class `az`/VS Code tooling, and it's what AZ-104 actually expects you to
author today over raw ARM JSON.

Scoped to just the App Service for now. The Booking Functions app (tasks 14-16) needs a materially
different deploy -- Azure Functions Consumption plan, a real Storage account, not Azurite -- and gets its
own module (`modules/functions.bicep`) when we get back to deploying that.

## Layout

- `main.bicep` -- **subscription-scoped**: creates the resource group itself, then deploys the module
  below into it. Nothing needs to pre-exist in the subscription.
- `main.bicepparam` -- parameter values for this deployment.
- `modules/app-service.bicep` -- **resource-group-scoped**: the App Service Plan (F1/Free, Windows) and
  the Web App itself.

## Before you deploy

- Azure CLI installed (`az --version`) and logged in (`az login`).
- Bicep CLI available (`az bicep version`; `az bicep install` if missing).
- `webAppName` (see `main.bicepparam`) must be **globally unique across all of Azure** -- it becomes
  `<name>.azurewebsites.net`. The default is `flightai-api-dev`; if deployment fails on a name conflict,
  uncomment and change the `webAppName` line in `main.bicepparam`.
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

The deployment outputs `webAppUrl` -- that's where the zip-deployed app will actually be reachable.
