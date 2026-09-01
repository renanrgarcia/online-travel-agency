# FlightAi

[![CI/CD](https://github.com/renanrgarcia/online-travel-agency/actions/workflows/ci-cd.yml/badge.svg)](https://github.com/renanrgarcia/online-travel-agency/actions/workflows/ci-cd.yml)

An AI-assisted flight search and booking system, built step by step as a learning exercise — both a
reference implementation of a specific architectural bet, and a hands-on path through Azure (this repo
doubles as AZ-104 study: Bicep, App Service, Functions, Static Web Apps, and the CI/CD that ties them
together).

**The central design bet:** keep search, ranking, pricing, and ticketing fully deterministic, and use
AI only at the two edges — turning a natural-language query into a structured search, and turning a
ranked, already-priced result set into readable prose. Nothing in between calls a model, and no model
output ever reaches a user without passing back through deterministic code first.

## Try it live

| Piece | URL |
|---|---|
| Frontend (chat UI) | https://victorious-meadow-0d0da3c03.3.azurestaticapps.net |
| Search API | https://flightai-api-dev.azurewebsites.net |
| Booking saga (Functions) | https://flightai-booking-dev.azurewebsites.net |

All three are real, deployed, free-tier Azure resources — not a mockup. One honest caveat: App Service's
F1 tier cold-starts after idling, so the first request can take a few seconds. All three redeploy
automatically on every merge to `main` (see [CI/CD and deployment](#cicd-and-deployment) below), so what's
live always matches `main` specifically — `develop` can be ahead of it between merges.

**Search** (streams four Server-Sent Events — parsed intent, one `supplier-result` per connector, ranked
offers, then an explanation):

```bash
curl -N --get "https://flightai-api-dev.azurewebsites.net/api/search/stream" \
  --data-urlencode "q=cheapest flight from São Paulo to Lisbon"
```

**Book** (starts the Durable Functions saga — payment, order, ticket, confirmation — then poll for the
result):

```bash
curl -X POST https://flightai-booking-dev.azurewebsites.net/api/bookings \
  -H "Content-Type: application/json" \
  -d '{"bookingId":"demo-001","offerId":"NDC-abc123","travellerEmail":"t@example.com","amount":791.00,"currency":"USD","paymentMethodToken":"tok_test"}'

curl https://flightai-booking-dev.azurewebsites.net/api/bookings/demo-001
```

An offer ID containing `FAIL-TICKET` (e.g. `NDC-FAIL-TICKET-xyz`) deterministically fails ticketing, so
you can watch the saga compensate — void the payment, cancel the order — instead of leaving a
charged-but-unfulfilled booking. Full contract: [`docs/reference/06-api-sse-contract.md`](docs/reference/06-api-sse-contract.md)
and [`docs/reference/07-booking-saga.md`](docs/reference/07-booking-saga.md).

## Tech stack

| Layer | Choice |
|---|---|
| Backend | .NET 10, ASP.NET Core Minimal APIs, Server-Sent Events |
| AI layer | `Microsoft.Agents.AI` + `Microsoft.Extensions.AI` — offline/deterministic stand-in today, real model swap-in is a planned task |
| Booking workflow | Azure Durable Functions (the saga pattern: checkpointed steps, each with a compensating action) |
| Frontend | React 19 + TypeScript + Vite, Vitest + Testing Library |
| Infrastructure as Code | Bicep, subscription-scoped (creates its own resource group) |
| CI/CD | GitHub Actions — test on every push/PR, deploy on push to `main` |
| Hosting | Azure Static Web Apps (Free), App Service (F1/Free), Functions (Consumption), Storage |
| Testing | xUnit (backend), Vitest (frontend) — both organized as one test per documented eval, not ad hoc |

Every Azure resource targets a free or near-free tier by design — see
[`docs/deployment.md`](docs/deployment.md) for the full cost breakdown and the one real trade-off it
forces (Azure's model-hosting layer has no perpetual free tier, so the AI edges run offline/deterministic
until that's deliberately swapped).

## What's built

Status reflects what's implemented in code today, on `develop` — the branch this table is meant to stay
current against. It can be ahead of what's live on `main` between merges; see
[CI/CD and deployment](#cicd-and-deployment).

**Backend** — [`docs/features/01-backend/`](docs/features/01-backend/README.md)

| Step | What | Status |
|---|---|---|
| 1. Price integrity core | Server-side price tokens; a model can reference a price, never author one | ✅ Done |
| 2. Ranking | Deterministic offer scoring | ✅ Done |
| 3. Suppliers | Mock GDS/NDC/LCC connectors, fan-out, budget + circuit breaker | ✅ Done |
| 4. AI layer, offline | Intent parsing and explanation, against a deterministic stand-in model | ✅ Done |
| 5. API + SSE | `GET /api/search/stream`, the full four-event pipeline | ✅ Done |
| 6. Decision support | Comparison facts (deltas, superlatives) for the explanation to state | ⬜ Not started |
| 7. Booking saga | Durable Functions saga, happy path + compensation + idempotency | ✅ Done |
| 8. Safe to expose | CORS, rate limiting, server-authoritative prices, structured error handling | ✅ Done |
| 9. Real model | Swap the offline stand-in for a real `IChatClient` | ⬜ Not started |

**Frontend** — [`docs/features/02-frontend/`](docs/features/02-frontend/README.md)

| Task | What | Status |
|---|---|---|
| F01 | Vite scaffold + typed SSE client | ✅ Done |
| F02 | Chat shell, EN/PT-BR toggle | ✅ Done |
| F03 | The search turn — real SSE stream driving the chat | ✅ Done |
| F04 | Offer cards and comparison | 🚧 In progress |
| F05 | The booking turn — saga from the chat UI, including compensation | ⬜ Not started |
| F06 | Degraded states | ⬜ Not started |
| F07 | Bilingual UI (beyond F02's toggle) | ⬜ Not started |

**Infrastructure** — [`docs/features/03-infra/`](docs/features/03-infra/README.md)

| Task | What | Status |
|---|---|---|
| 01 | Functions infra (Consumption plan, Storage) + CI/CD | ✅ Done, live |
| 02 | Static Web App + CORS wiring for both backends | ✅ Done, live |

## Repository layout

```
backend/            .NET 10 -- the solution lives here, not at the repo root.
  FlightAi.slnx     Covers only the .NET projects.
  src/
    FlightAi.Core/          Domain + deterministic logic. No AI dependency at all.
    FlightAi.Agents/        The AI layer (the only project that touches a model).
    FlightAi.Api/           Minimal API -- GET /api/search/stream (Server-Sent Events).
    FlightAi.Booking.Functions/  Azure Durable Functions booking saga.
  tests/
    FlightAi.Tests/         xUnit. One test per documented eval, see docs/features/01-backend/tasks/.
frontend/           React + TypeScript + Vite SPA -- a chat interface over the backend.
infra/              Bicep. Subscription-scoped, provisions the Azure resources.
.github/workflows/  CI on develop + PRs, deploy on push to main.
docs/
  reference/        How the system works, in reading order.
  features/         The build spec: a roadmap and scoped tasks with evals, per feature.
  deployment.md     Azure topology, free-tier constraints, deployment order.
```

## Running it locally

**Backend API** (`http://localhost:5294`):

```bash
dotnet run --project backend/src/FlightAi.Api
```

**Booking Functions** — needs [Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite)
(Durable Task's local storage emulator) and [Azure Functions Core Tools](https://learn.microsoft.com/azure/azure-functions/functions-run-local):

```bash
azurite --skipApiVersionCheck &
cd backend/src/FlightAi.Booking.Functions && func start   # http://localhost:7071
```

**Frontend** (`http://localhost:5173`) — the API's URL is build-time configuration, never hardcoded
(`frontend/src/config.ts`), so point it at the API above explicitly; `appsettings.Development.json`
already allows this origin in CORS:

```bash
cd frontend && npm install
echo "VITE_API_BASE_URL=http://localhost:5294" > .env.development
npm run dev
```

## Testing

```bash
dotnet test backend/FlightAi.slnx     # backend -- xUnit
cd frontend && npm test               # frontend -- Vitest + Testing Library
```

Both suites are organized as one test per documented eval rather than ad hoc coverage — every task card
under `docs/features/*/tasks/` lists its evals with an ID and the reason each one exists, and the
matching test file references those IDs directly. See
[`docs/features/README.md`](docs/features/README.md) for the discipline behind this.

## CI/CD and deployment

Push to `develop` or open a PR against `main` → `build-and-test` (backend) and `build-and-test-frontend`
run; both are required checks. Push to `main` → three deploy jobs run in parallel: the API and Functions
publish via their Azure publish profiles, the frontend builds and deploys to Static Web Apps with its API
and Functions base URLs supplied as build-time variables (sourced from the same Bicep outputs that
provisioned them, not duplicated by hand).

Infrastructure itself is provisioned separately, by running Bicep directly against the subscription —
see [`infra/README.md`](infra/README.md) for the exact commands and the region/quota caveat that shaped
`main.bicepparam`. [`docs/deployment.md`](docs/deployment.md) covers the full topology and cost
reasoning; [`docs/features/03-infra/README.md`](docs/features/03-infra/README.md) covers why
infrastructure is its own feature rather than folded into backend or frontend.

## Where to go deeper

Start at [`docs/README.md`](docs/README.md) for how the spec is organized, then
[`docs/reference/`](docs/reference/README.md) to understand the system end to end, then pick a feature
roadmap: [backend](docs/features/01-backend/README.md), [frontend](docs/features/02-frontend/README.md),
[infra](docs/features/03-infra/README.md).
