# docs

The standalone spec for an AI-assisted flight search and booking system. This folder is
self-contained — it doesn't reference or depend on any external document. It exists so the design
decisions behind the implementation survive independently of the code itself.

It's split three ways, by the question each part answers:

| Folder | Answers | Read it when |
|---|---|---|
| [`reference/`](reference/README.md) | *How does the system work?* | Understanding what exists, or rebuilding a piece |
| [`features/`](features/README.md) | *What do I build next, and how do I know it's right?* | Implementing — each feature carries scoped tasks with evals |
| [`deployment.md`](deployment.md) | *How does this reach Azure, at what cost?* | Deploying any part of it |

## reference/ — the system as designed

Eleven documents, in reading order, each naming the source files it describes. This is the layer that
explains *why* — why ranking isn't a model call, why a token vocabulary exists, why the booking flow
is a saga. Start at [`reference/01-architecture-overview.md`](reference/01-architecture-overview.md).

## features/ — the build spec, per feature

The same material reordered into *build* order and split by feature. Each feature folder has a
README (its roadmap) and a `tasks/` folder of individually scoped, testable cards, every one carrying
an **Evals** table written before the implementation exists.

- [`features/01-backend/`](features/01-backend/README.md) — .NET 10: deterministic core, AI edges,
  streaming API, booking saga.
- [`features/02-frontend/`](features/02-frontend/README.md) — React + TypeScript: the chat interface
  that makes the backend's streamed pipeline something a traveller can actually use.
- [`features/03-infra/`](features/03-infra/README.md) — Bicep + GitHub Actions for the Booking
  Functions app and the frontend's Static Web App.

Features are numbered in the order they were specified, not a strict dependency order — the frontend
depends on the backend's API contract, but the two can be built in parallel once that contract exists.

## deployment.md — Azure, end to end

Target topology, free-tier constraints, and the deployment order. Individual tasks carry
**Deployment gate** sections with the acceptance criteria for the step they unlock.

## archive/ — superseded material

Pre-restructuring content kept for reference, not part of the current spec — see
[`archive/README.md`](archive/README.md). If it disagrees with anything above, this structure wins.
