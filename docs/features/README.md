# features — the build spec

Each feature is a coherent, independently buildable slice of the system, with its own roadmap and its
own numbered task cards. [`../reference/`](../reference/README.md) explains how the system works;
this folder is what you actually work through.

| Feature | Scope | Tasks |
|---|---|---|
| [01 — backend](01-backend/README.md) | .NET 10. Deterministic core, the two AI edges, the streaming search API, the Durable Functions booking saga. | [`01-backend/tasks/`](01-backend/tasks/README.md) |
| [02 — frontend](02-frontend/README.md) | React + TypeScript + Vite. A chat interface over the backend's SSE search stream and booking saga. | [`02-frontend/tasks/`](02-frontend/tasks/README.md) |

## The eval discipline

Every task carries an **Evals** table: numbered acceptance criteria, each a fixed input and an
expected observable output, written *before* the implementation exists. Tests assert exactly these,
and test names carry the eval ID (`E1_...`, `E2_...`) so a failure points at the criterion it violates.

This ordering is the whole point. A test written after the code tends to assert whatever the code
already does — it passes by construction and proves nothing. An eval written first is an external
target the implementation has to meet, so when the two disagree, **the implementation is wrong, not
the eval.**

Each task also has a **Locked decisions** section recording choices the source docs left open. If you
disagree with a locked decision, change it in the task card first, then let the failing test drive the
code change.

## How the two features relate

The frontend depends on one thing from the backend: the SSE contract in
[`../reference/06-api-sse-contract.md`](../reference/06-api-sse-contract.md), plus the booking saga's
HTTP contract in [`../reference/07-booking-saga.md`](../reference/07-booking-saga.md). Once those
exist and are stable, the two features can progress in parallel — which is why they're separate
features rather than one long list.

Nothing about the backend depends on the frontend existing. The frontend is what makes the backend's
per-stage streaming *visible*: without it, the `parsed-intent`, `supplier-result`, and `ranked-offers`
events are only ever observed by `curl` and the test suite.
