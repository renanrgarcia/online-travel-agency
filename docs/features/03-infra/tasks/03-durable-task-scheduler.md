# 03 — Durable Task Scheduler

**Roadmap step:** 3. Durable Task Scheduler
**Source doc:** `infra/README.md`; [Durable Task Scheduler](https://learn.microsoft.com/en-us/azure/durable-task/scheduler/durable-task-scheduler),
[billing](https://learn.microsoft.com/en-us/azure/durable-task/scheduler/durable-task-scheduler-billing),
[managed identity](https://learn.microsoft.com/en-us/azure/durable-task/scheduler/durable-task-scheduler-identity)
**Depends on:** Infra task 01 (the Function App this extends). Not blocked on backend task 24's code —
the resource itself can be provisioned independently, same as every other infra task here — but backend
task 24's deployment gate depends on this one being live.

## Goal

Real Cost Management data (queried directly, not estimated) showed the Storage account backing
`FlightAi.Booking.Functions` costing a flat ~R$0.15–0.20/day regardless of actual booking volume — the
Durable Task extension's default Azure Storage provider polls its control queue on a timer, 24/7, whether
or not there's anything in it. Provision a Durable Task Scheduler (Consumption SKU) resource instead: a
dispatch-driven backend with no polling loop and, per the Retail Prices API, no base fee at all
($0.00429 per million actions dispatched, `westeurope`) — the property that actually fixes this, as
opposed to task 20's rate limiting or the two operational dials (stop the app between uses; back off
`host.json`'s polling interval) that were the only options before this was found.

## Scope

- A new `infra/modules/durable-task-scheduler.bicep` module: the scheduler resource
  (`Microsoft.DurableTask/schedulers` — confirm exact resource type casing and current API version at
  implementation time, not assumed from this card) on the Consumption SKU, plus a task hub.
- **Managed identity wiring** — this resource authenticates callers via Azure RBAC, not a connection
  string with an embedded key, unlike every other secret this project has wired so far
  (`PriceAssertion:SigningKey`, `Gemini:ApiKey`). Needs: a user-assigned managed identity (Microsoft's own
  guidance prefers user-assigned over system-assigned here, since it isn't tied to the Function App's own
  lifecycle), assigned to the Function App, and a role assignment granting that identity the
  **Durable Task Data Contributor** role, scoped to the task hub. This is genuinely new territory for this
  project's Bicep — everything before this has been a plain `@secure()` string in an app setting.
- Thread the scheduler's endpoint into `functions.bicep`'s Function App as the
  `DURABLE_TASK_SCHEDULER_CONNECTION_STRING` app setting (or whatever backend task 24 confirms is
  current) — an identity-based connection string, not a secret value, so this one does **not** follow the
  `readEnvironmentVariable`-from-`infra/.env` pattern the other two secrets use.
- Update `main.bicep` to wire the new module in, and `infra/README.md`'s resource inventory and Secrets
  section to describe this one accurately (it isn't a secret in the same sense as the other two).

## Out of scope

- **Removing the existing Storage account** (`flightaifuncsdev`). It stays — `AzureWebJobsStorage` (the
  Functions host's own key management and coordination requirement) is a separate need from Durable
  Task's control-queue polling, and exists regardless of which Durable Task provider is in use. This task
  eliminates the `Queues v2` cost driver specifically, not the Storage account itself.
- **The Dedicated SKU** — see Locked decisions.
- **Backend task 24's code changes** (the NuGet package, `host.json`, the orchestrator's own logic) —
  this task provisions the resource and the identity/RBAC wiring around it; the application-side
  configuration is that task's scope.

## Evals

| ID | Setup | Expected | Why it matters |
|---|---|---|---|
| E1 | `az bicep build` on the new module and `main.bicep` | Clean | Same pre-flight discipline as every infra task here |
| E2 | `az deployment sub what-if` | Shows the scheduler resource, task hub, managed identity, and role assignment created, plus the Function App gaining the new connection app setting — nothing else disturbed | The real check. Given this project's own repeated experience with `what-if` false positives on nested `siteConfig` properties (see `infra/README.md`), verify anything unexpected against the live resource directly before treating it as a real finding |
| E3 | `az deployment sub create` | Succeeds; the Function App's app settings show the new connection value; the role assignment is visible under the task hub's Access control (IAM) | Confirms the identity-based wiring actually applied, not just the resource existing |
| E4 | Real Cost Management data, checked a few days after backend task 24's deploy (not assumed from the pricing model) | The Storage account's `Queues v2` meter stops accruing new daily cost | The actual point of this task — E1–E3 prove the resource is correctly provisioned, E4 proves it solved the problem |

### Locked decisions

- **Consumption SKU only.** Dedicated bills a fixed hourly rate per Capacity Unit regardless of usage
  ($0.82–$1.27/hour depending on region) — reintroducing exactly the kind of flat idle cost this task
  exists to eliminate, just on a different resource.
- **User-assigned managed identity, not a connection-string key.** Matches Microsoft's own current
  guidance for this resource, and means there's no new secret to generate, rotate, or keep out of git —
  a genuinely better security posture than the pattern used for `PriceAssertion:SigningKey`, not just a
  different one.
- **Existing Storage account is untouched.** See Out of scope.

## Done when

All four evals pass, and backend task 24's own deployment gate (a real booking completing end to end
against the deployed Function App, on this resource) passes too — this task alone doesn't prove anything
end to end by itself, it only proves the resource is correctly provisioned and reachable.
