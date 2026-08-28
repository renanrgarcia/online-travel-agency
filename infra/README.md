# Infrastructure as Code

Bicep, not Terraform or hand-written ARM JSON -- see the reasoning in this project's chat history if you
want it, but short version: no state file to manage (Azure Resource Manager tracks deployments itself),
first-class `az`/VS Code tooling, and it's what AZ-104 actually expects you to author today over raw ARM
JSON.

Scoped to just the App Service for now. The Booking Functions app (tasks 14-16) needs a materially
different deploy -- Azure Functions Consumption plan, a real Storage account, not Azurite -- and gets its
own module (`modules/functions.bicep`) when we get back to deploying that.

## Layout

- `main.bicep` -- orchestrates the modules below, resource-group scoped.
- `main.bicepparam` -- parameter values for this deployment.
- `modules/app-service.bicep` -- the App Service Plan (F1/Free, Windows) and the Web App itself.

## Before you deploy

- Azure CLI installed and logged in (`az login`).
- Bicep CLI available (`az bicep install` if `az bicep version` doesn't already show one -- recent `az`
  versions bundle it).
- A resource group to deploy into. Create one if you don't have one yet:

  ```bash
  az group create --name rg-flightai-dev --location eastus
  ```

- `webAppName` in `main.bicepparam` (via `main.bicep`'s `environmentName` -> `flightai-api-dev`) must be
  **globally unique across all of Azure** -- it becomes `<name>.azurewebsites.net`. If deployment fails
  on a name conflict, change `environmentName` in `main.bicepparam` to something more distinctive.
- `netFrameworkVersion` in `modules/app-service.bicep` defaults to `'v10.0'` but has **not** been verified
  against a live subscription -- .NET 10 is new enough that this exact string isn't confirmed yet. Check
  what's actually available before deploying:

  ```bash
  az webapp list-runtimes --os windows | grep -i dotnet
  ```

  and adjust the `netFrameworkVersion` parameter if the listed string differs.

## Deploy

Preview what would change first -- this is the whole point of Bicep not needing a state file, you get a
real diff against what's actually in Azure right now:

```bash
az deployment group what-if \
  --resource-group rg-flightai-dev \
  --template-file main.bicep \
  --parameters main.bicepparam
```

Then apply:

```bash
az deployment group create \
  --resource-group rg-flightai-dev \
  --template-file main.bicep \
  --parameters main.bicepparam
```

The deployment outputs `webAppUrl` -- that's where the zip-deployed app will actually be reachable.
