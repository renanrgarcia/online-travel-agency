# Frontend tasks

Numbered in the order to implement them — the same order as [`../README.md`](../README.md), this
feature's roadmap. The eval discipline these cards follow is described once, in
[`../../README.md`](../../README.md).

| # | Task | Roadmap step |
|---|---|---|
| [01](01-scaffold-and-sse-client.md) | Scaffold and typed SSE client | 1. Foundations |
| [02](02-chat-shell.md) | Chat shell | 1. Foundations |
| [03](03-the-search-turn.md) | The search turn | 2. The search turn |
| [04](04-offer-cards-and-comparison.md) | Offer cards and comparison | 2. The search turn |
| [05](05-the-booking-turn.md) | The booking turn | 3. The booking turn |
| [06](06-degraded-states.md) | Degraded states | 4. Honesty and reach |
| [07](07-bilingual-ui.md) | Bilingual UI | 4. Honesty and reach |
| [08](08-static-web-apps-deployment.md) | Static Web Apps deployment | 5. Deployment |

## Testing these

The backend's evals run under xUnit. These run under **Vitest** with **Testing Library**, asserting on
what a user can observe — rendered text and roles — rather than on component internals, so a refactor
that preserves behaviour doesn't break the suite.

Two things make this tractable without a running backend:

- **The SSE contract is a fixed, documented byte format.** A test can feed a hand-written
  `event:`/`data:` stream through the real client (task 01) and assert on what the UI does with it, with
  no server involved.
- **The backend is deterministic.** The same query against the mock connectors always produces the same
  offers in the same order, so a fixture recorded from a real run stays valid indefinitely — it can't
  silently drift the way a fixture recorded from a live third-party API would.

An end-to-end check against a locally running `FlightAi.Api` is worth doing at tasks 03 and 05, but it
is not what the eval suite depends on.

## A note on dependencies

Nothing here needs a component library, a state management library, or a CSS framework. The backend
feature earned its dependencies by needing them; this one shouldn't acquire any by default. If a task
turns out to genuinely need one, that's a decision to record in that task's **Locked decisions**, not a
default to assume.
