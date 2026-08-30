# Feature 03 — infrastructure

Bicep and GitHub Actions CI/CD for the pieces of the system that don't yet have either: the Booking
Functions app and the frontend's Static Web App. [`infra/`](../../../infra/README.md) already covers
`FlightAi.Api` (`infra/modules/app-service.bicep`), and `.github/workflows/ci-cd.yml` already builds,
tests, and deploys it — that groundwork isn't repeated here, only extended.

## Why a separate feature, not a task inside backend or frontend

These two tasks provision Azure resources and wire deploy jobs — a different *kind* of work from
application code, with its own tooling (Bicep, `az`), its own failure modes (quota, region availability,
naming collisions — see `infra/main.bicepparam`'s own history for a real one), and its own review
concerns (cost, least-privilege credentials). Each still depends on real application code from its own
feature (see each task's **Depends on**), but authoring and validating a Bicep module doesn't require
touching that feature's codebase at all — `az bicep build` and `az deployment sub what-if` both work
against a template alone. Separating them out means infrastructure can be written and validated ahead of
the application code being ready, then tested against once it is, rather than the two being forced to
land together.

## Roadmap

### 1. Functions infrastructure and CI/CD

**Tasks:** 01 · originally backend task 22

Consumption plan Function App, plus the Storage account Durable Task requires, provisioned by Bicep; a
deploy job for `FlightAi.Booking.Functions`. Depends on backend task 16 (the saga's application code) —
not on task 02 below.

### 2. Static Web Apps deployment

**Tasks:** 02 · originally frontend task F08

The Static Web App, provisioned by Bicep; a deploy job that builds the frontend and publishes it, with
the API base URL supplied as build configuration. Depends on frontend task 03 at minimum, and backend
task 19 (CORS) — not on task 01 above.

## Cost

Every resource here targets a free or near-free tier — see [`docs/deployment.md`](../../deployment.md)'s
topology table and each task's own notes. Static Web Apps Free and Functions Consumption's monthly grant
are both $0 at demo volume; the one unavoidable line item is the Storage account Durable Task needs,
which `docs/deployment.md` already prices at cents per month, not dollars. Nothing here should ever
produce a surprise bill — if a `what-if` ever shows a SKU or tier you don't recognize, stop and check it
against this before applying.
