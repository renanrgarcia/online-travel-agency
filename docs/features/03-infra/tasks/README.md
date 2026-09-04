# Infra tasks

Numbered in the order to implement them. Each was originally a task inside the feature it provisions
infrastructure for (see each card's header) — moved here once it became clear both were the same *kind*
of work (Bicep + a CI/CD deploy job), independent of which feature's application code they ultimately
serve. Their dependencies still point back at real backend/frontend tasks; nothing about the move changes
what has to exist first.

The eval discipline these cards follow is described once, in [`../../README.md`](../../README.md) — read
it before writing a test against either.

| # | Task | Originally |
|---|---|---|
| [01](01-functions-infrastructure-and-cicd.md) | Functions infrastructure and CI/CD | Backend task 22 |
| [02](02-static-web-apps-deployment.md) | Static Web Apps deployment | Frontend task F08 |
| [03](03-durable-task-scheduler.md) | Durable Task Scheduler | — (written directly here; pairs with backend task 24, not a moved task) |
| [04](04-duffel-api-key.md) | Duffel API key | — (written directly here; pairs with backend task 25, not a moved task) |

## Testing these

`az bicep build` / `az bicep build-params` play the role `dotnet test` and Vitest play elsewhere: the
check that needs no live subscription, so it comes first. `az deployment sub what-if` is the one that
does — the real pre-flight, since it asks Azure itself what would happen rather than guessing. Both
should be clean before `az deployment sub create` ever runs.
