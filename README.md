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
    FlightAi.Demo/          Console app wiring the pipeline into one run.
    FlightAi.Api/           Minimal API — GET /api/search/stream (Server-Sent Events).
    FlightAi.Booking.Functions/  Azure Durable Functions booking saga.
  tests/
    FlightAi.Tests/         xUnit. Eval-aligned tests, see docs/specs/tasks/.
frontend/           React + TypeScript + Vite SPA.
docs/               The standalone spec, the dossier, and the build-order roadmap.
  specs/            Implementation roadmap + 17 scoped tasks with evals.
```

Only `backend/` is covered by the .NET solution. `frontend/` is a separate npm project, and the two
are deployed independently (Azure Static Web Apps for the frontend, App Service / Functions for the
backend) — see `docs/specs/deployment.md`.

## Where to start

Read [`docs/README.md`](docs/README.md) for the system spec, then
[`docs/specs/macro-scenario.md`](docs/specs/macro-scenario.md) for the build-order roadmap, then work
through [`docs/specs/tasks/`](docs/specs/tasks/README.md) starting at task 01.

## Running what exists today

```bash
dotnet test backend/FlightAi.slnx
```
