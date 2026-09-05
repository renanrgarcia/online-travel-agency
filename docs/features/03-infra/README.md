# Feature 03 — infrastructure

Bicep and GitHub Actions CI/CD for the pieces of the system that don't yet have either, plus infra-level
fixes to what's already deployed once real operation surfaces something worth fixing.
[`infra/`](../../../infra/README.md) already covers `FlightAi.Api` (`infra/modules/app-service.bicep`),
and `.github/workflows/ci-cd.yml` already builds, tests, and deploys it — that groundwork isn't repeated
here, only extended.

## Why a separate feature, not a task inside backend or frontend

These tasks provision Azure resources and wire deploy jobs — a different *kind* of work from application
code, with its own tooling (Bicep, `az`), its own failure modes (quota, region availability, naming
collisions — see `infra/main.bicepparam`'s own history for a real one), and its own review concerns
(cost, least-privilege credentials). Each still depends on real application code from its own feature
(see each task's **Depends on**), but authoring and validating a Bicep module doesn't require touching
that feature's codebase at all — `az bicep build` and `az deployment sub what-if` both work against a
template alone. Separating them out means infrastructure can be written and validated ahead of the
application code being ready, then tested against once it is, rather than the two being forced to land
together.

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

### 3. Durable Task Scheduler

**Tasks:** 03 · pairs with backend task 24

Task 01 above provisioned the Booking Functions app on the default Azure Storage backend, which turned
out to carry a real cost this project didn't originally account for — see Cost below. This task replaces
that backend with the Durable Task Scheduler (Consumption SKU): a dispatch-driven resource with no base
fee, provisioned alongside a user-assigned managed identity and an RBAC role assignment rather than a
plain secret, which is new territory for this project's Bicep. Depends on task 01 (the Function App this
extends), not on task 02.

### 4. Duffel API key

**Tasks:** 04 · pairs with backend task 25

Threads a Duffel test-mode token into `FlightAi.Api`'s configuration, repeating the exact
`geminiApiKey` pattern (empty default, App Service Configuration, never Key Vault at this scale) for a
second real dependency. Depends on nothing structurally new — backend task 25's own deployment gate
depends on this one, not the reverse.

## Cost

Every resource here targets a free or near-free tier — see [`docs/deployment.md`](../../deployment.md)'s
topology table and each task's own notes. Static Web Apps Free and Functions Consumption's monthly grant
are both genuinely $0 at demo volume, confirmed against the live Retail Prices API, not assumed. The
Storage account Durable Task needs was, until task 03 above, the one real line item — a **flat daily cost
from the Azure Storage backend's own background polling** (roughly R$0.15–0.20/day, observed via real
billing data, present even with zero usage), not the "cents per month, scales with usage" estimate this
section originally carried. Task 03 replaces that backend specifically because it has no equivalent
polling cost — confirmed at $0.00429 per million actions with no base fee, also via the live Retail
Prices API. Nothing here should ever produce a *surprise* bill regardless of any of this — a budget with
four notification thresholds is configured on the subscription specifically so a drift from any baseline
shows up as an email, not a bill you find later. If a `what-if` ever shows a SKU or tier you don't
recognize, stop and check it against this before applying.
