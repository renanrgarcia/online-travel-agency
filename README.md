# online-travel-agency

An AI-assisted flight search and booking system, built step by step as a learning exercise.

The central design bet: keep search, ranking, pricing, and ticketing fully deterministic, and use AI
only at the two edges — turning a natural-language query into a structured search, and turning a
ranked, already-priced result set into readable prose. Nothing in between calls a model.

## Repository layout

```
backend/            .NET 10 — the solution lives here, not at the repo root.
  FlightAi.slnx     Covers only the .NET projects.
  src/
    FlightAi.Core/          Domain + deterministic logic. No AI dependency at all.
    FlightAi.Agents/        The AI layer (the only project that touches a model).
    FlightAi.Api/           Minimal API — GET /api/search/stream (Server-Sent Events).
    FlightAi.Booking.Functions/  Azure Durable Functions booking saga.
  tests/
    FlightAi.Tests/         xUnit. Eval-aligned tests, see docs/features/01-backend/tasks/.
frontend/           React + TypeScript + Vite SPA — a chat interface over the backend.
infra/              Bicep. Subscription-scoped, provisions the Azure resources.
.github/workflows/  CI on develop + PRs, deploy on push to main.
docs/
  reference/        How the system works, in reading order.
  features/         The build spec: a roadmap and scoped tasks with evals, per feature.
  deployment.md     Azure topology, free-tier constraints, deployment order.
```

Only `backend/` is covered by the .NET solution. `frontend/` is a separate npm project, and the two
are deployed independently (Azure Static Web Apps for the frontend, App Service / Functions for the
backend) — see `docs/deployment.md`.

## Where to start

Read [`docs/README.md`](docs/README.md) for how the spec is organised, then
[`docs/reference/`](docs/reference/README.md) to understand the system, then pick a feature:

- [`docs/features/01-backend/`](docs/features/01-backend/README.md) — the .NET side, tasks 01–22.
- [`docs/features/02-frontend/`](docs/features/02-frontend/README.md) — the chat interface, tasks F01–F08.

## Running what exists today

```bash
dotnet test backend/FlightAi.slnx
```
