# 22 — Functions infrastructure and CI/CD

**Roadmap step:** 10. Infrastructure completion
**Source doc:** `docs/deployment.md`, `infra/README.md`
**Depends on:** 16

## Goal

Bring the booking Functions app under the same Bicep and GitHub Actions treatment the API already has,
so every deployed piece is reproducible from source rather than clicked together in the portal once and
forgotten.

`infra/` currently provisions the App Service and nothing else — its own README says
`modules/functions.bicep` arrives "when we get back to deploying that." `.github/workflows/ci-cd.yml`
likewise builds and tests the whole solution but only publishes `FlightAi.Api`. Task 16's deployment
gate (D1) requires a real Function App with a real Storage account; without this task that happens by
hand and exists nowhere in the repository.

## Scope

- `infra/modules/functions.bicep` — Consumption plan, Function App, and the Storage account Durable
  Task requires, wired into `main.bicep`.
- A deploy job in the existing workflow that publishes `FlightAi.Booking.Functions` on push to `main`.
- The Function App's CORS configured as infrastructure (see task 19 — a separate host is a separate
  origin, and the frontend calls both).

## Out of scope

- Static Web Apps infrastructure — that belongs to the frontend feature, which owns what gets deployed
  there.
- Key Vault. Configuration app settings are sufficient at this scale; task 17 already documents the
  key-handling rule that matters.

## Evals

| ID | Setup | Expected | Why it matters |
|---|---|---|---|
| E1 | `az bicep build` on the new module and on `main.bicep` | Clean, no warnings | Catches wrong resource types, API versions, and type mismatches with no subscription involved |
| E2 | `az deployment sub what-if` against a subscription | Shows exactly the Function App, plan, and Storage account being created, and nothing unexpected being modified or deleted | `what-if` is the real pre-flight check — the one that asks Azure rather than guessing |
| E3 | Deploy, then re-run the same deployment unchanged | Second run reports no changes | Idempotence. A template that isn't safe to re-run isn't infrastructure as code, it's a one-shot script |
| E4 | The deployed Function App's `AzureWebJobsStorage` | Points at the Storage account the template created | The single most common Durable Functions misconfiguration, and it fails at runtime rather than at deploy |
| E5 | Push to `main` | Both the API and the Functions app publish; `build-and-test` still gates both | One green pipeline, not one deployed piece and one manual one |
| E6 | The repository, grepped | No storage connection string, key, or publish profile committed | The rule from task 17, applied to a second set of secrets |
| E7 | Task 16's curl examples against the deployed Function App | Happy path and compensation path both behave as they did on Azurite | Deploying the infrastructure correctly and deploying *working software* are different claims |

### Locked decisions

- **The Storage account is created by the template**, not referenced as pre-existing — otherwise the
  deployment isn't self-contained and task 14's "nothing needs to pre-exist" property is lost.
- **Consumption plan**, per `docs/deployment.md`'s topology and its free monthly grant.

## Done when

All seven evals pass, and `infra/README.md` documents the Functions module the way it documents the App
Service one.
