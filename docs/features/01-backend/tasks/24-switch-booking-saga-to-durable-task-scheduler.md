# 24 — Switch the booking saga to the Durable Task Scheduler

**Roadmap step:** 7. Booking saga
**Source doc:** `docs/reference/07-booking-saga.md`; [Durable Task Scheduler](https://learn.microsoft.com/en-us/azure/durable-task/scheduler/durable-task-scheduler),
[quickstart](https://learn.microsoft.com/en-us/azure/durable-task/scheduler/quickstart-durable-task-scheduler),
[billing](https://learn.microsoft.com/en-us/azure/durable-task/scheduler/durable-task-scheduler-billing),
[managed identity](https://learn.microsoft.com/en-us/azure/durable-task/scheduler/durable-task-scheduler-identity)
**Depends on:** Backend task 16 (the saga's business logic — unchanged by this task). Pairs with infra
task 03, which provisions the live resource this task's deployed config points at.

## Goal

Stop paying for the default Azure Storage backend's constant control-queue polling — confirmed via real
Cost Management data to be a flat ~R$0.15–0.20/day (~R$5–6/month), present even with zero bookings,
because the Durable Task extension polls that queue on a timer regardless of whether there's anything in
it. Switch `FlightAi.Booking.Functions` to the Durable Task Scheduler (Consumption SKU) instead — a
Microsoft-recommended, dispatch-driven backend with no polling loop and no base fee: confirmed via the
Retail Prices API at $0.00429 per million actions (`westeurope`), which puts this project's actual usage
at a fraction of a cent regardless of how much manual testing happens.

This task is configuration and wiring, not a rewrite. The orchestrator, activities, and compensation
logic in `BookingOrchestrator.cs`/`BookingActivities.cs` don't change at all — Durable Task's storage
provider is an abstraction specifically so this kind of swap doesn't touch business logic.

## Scope

- Add the `Microsoft.Azure.Functions.Worker.Extensions.DurableTask.AzureManaged` NuGet package.
  Confirm the exact version at implementation time — the quickstart above installed it `--prerelease`,
  which won't be true forever. Requires `Microsoft.Azure.Functions.Worker.Extensions.DurableTask` 1.2.2
  or higher; this project is already on 1.18.0, so the existing package stays, this one is additive.
- `host.json`: add a `storageProvider` block under `extensions.durableTask` pointing at
  `type: "azureManaged"`, reading its connection from a named setting (the quickstart's own sample uses
  `DURABLE_TASK_SCHEDULER_CONNECTION_STRING` — confirm this is still the current convention, not assumed).
- `local.settings.json`: point that same setting at the local emulator
  (`Endpoint=http://localhost:8080;TaskHub=default;Authentication=None`) — the emulator is a Docker
  container, needs documenting in `docs/reference/07-booking-saga.md`'s "Trying it locally" section
  alongside the existing Azurite instructions, and its dashboard (`http://localhost:8082`) is worth
  knowing about for debugging.
- For the deployed setting (infra task 03 supplies the actual value): the production connection string
  is authenticated via **managed identity, not a key** — this is a real difference from every other
  secret in this project (`PriceAssertion:SigningKey`, `Gemini:ApiKey`), which are plain `@secure()`
  strings. Confirm the exact production connection string shape and the managed identity wiring with
  infra task 03 rather than assuming it mirrors the local emulator's `Authentication=None` form.

## Out of scope

- **Migrating existing orchestration history.** Microsoft's own docs are explicit: you can't migrate data
  between Durable Task storage providers. This is a clean cutover — any bookings that existed on the old
  Azure Storage backend stop being queryable through the new one. Not a real concern for this project (no
  production data), but worth stating so nobody goes looking for old orchestration IDs after the switch.
- **The saga's business logic.** Orchestrator steps, compensation, idempotency (`bookingId` as instance
  ID) — none of it changes. If any of it needs to change to make this work, that's a sign something about
  this task's scope assumption is wrong, not a green light to also refactor the saga while in here.
- **The API's `PriceAssertion` signing key flow.** Unrelated system, unrelated task.
- **The Dedicated SKU.** Consumption only — see Locked decisions.

## Evals

| ID | Setup | Expected | Why it matters |
|---|---|---|---|
| E1 | `dotnet build` | Clean, with the new package reference resolved | Baseline |
| E2 | Local run against the Durable Task Scheduler emulator, happy-path booking (`docs/reference/07-booking-saga.md`'s documented example) | Completes exactly as it does today against Azurite — same `runtimeStatus`, same output shape | Proves the swap is transparent to callers |
| E3 | Local run, an offer ID containing `FAIL-TICKET` | Compensates exactly as documented — `VoidPayment` and `CancelOrder` both fire | Proves compensation logic is backend-agnostic, not something that happened to depend on Storage Queue ordering |
| E4 | The same `bookingId` submitted twice | Joins the existing instance rather than creating a second (task 16's idempotency guarantee) | Confirms instance-ID-based idempotency holds under the new provider too |
| E5 | `host.json` and the deployed app settings, inspected | No reference to a Storage-based control queue or history table for orchestration state remains; `AzureWebJobsStorage` is still present (the Functions host's own separate requirement, not what this task removes) | The eval that stops this task from being declared done on a partial cutover |

### Locked decisions

- **Consumption SKU, not Dedicated.** Dedicated bills a fixed hourly rate per Capacity Unit
  ($0.82–$1.27/hour depending on region, per the Retail Prices API) — that's a far bigger bill than the
  ~R$5–6/month problem this task exists to solve. Consumption's "no upfront costs, minimum commitments,
  or base fees" model is the entire point.
- **Clean cutover, not a dual-write migration.** Accepting the loss of pre-cutover demo orchestration
  history is the right trade for a portfolio project; building a migration path would be solving a
  problem this project doesn't have.

## Deployment gate

Depends on infra task 03 having provisioned the live Scheduler resource and wired its connection into
this app's configuration.

| ID | Requirement |
|---|---|
| D1 | The deployed Function App's configuration points at the live Durable Task Scheduler resource from infra task 03, not the emulator or the old Storage-based provider |
| D2 | A real booking against the **deployed** Function App completes end to end on the new backend — not just the local emulator |
| D3 | Real Cost Management data, checked a few days after cutover (not assumed from the pricing model alone), confirms the Storage account's `Queues v2` meter stops accruing new daily cost |

## Done when

All five evals and all three deployment gates pass. D3 in particular is the actual proof this task
accomplished what it set out to do — everything before it establishes correctness, D3 establishes that
the cost problem is actually gone.
