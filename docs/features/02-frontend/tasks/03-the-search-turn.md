# F03 — The search turn

**Roadmap step:** 2. The search turn
**Source doc:** `docs/reference/06-api-sse-contract.md`
**Depends on:** F01, F02, **backend 19 (CORS)**

## Goal

Join the two halves: a real search stream driving a real chat turn, each of the four events landing as
its own visible moment.

This is the task that makes the backend's streaming design pay off. Everything before it could have been
built against a plain JSON endpoint.

## Scope

- Submitting a message opens the stream; each event updates its stage of the in-flight assistant turn.
- The four stages: parsed intent as a compact confirmation, supplier results as a live status strip,
  ranked offers as a list, explanation as the assistant's prose.
- Configurable API base URL — the deployed frontend and API are on different origins.

## Out of scope

- Offer card detail and comparison — F04.
- Failure and degraded rendering — F06. This task assumes the happy path and may render failures crudely.

## Evals

| ID | Setup | Expected | Why it matters |
|---|---|---|---|
| E1 | Submit the demo query against a running API | All four stages populate, in contract order, and the turn completes | End to end for the first time |
| E2 | Suppliers responding at different speeds | Each `supplier-result` appears as it arrives — not all at once at the end | The single most important behaviour in this feature, and the one that silently degrades to a batch render if any layer buffers |
| E3 | While the stream is open | The turn shows it's still working, and which stage is outstanding | Otherwise a slow explanation is indistinguishable from a hung page |
| E4 | `parsed-intent` | Rendered as human-readable confirmation (origin, destination, date, travellers), not raw JSON | This is the system showing it understood — the first half of the AI sandwich, made legible |
| E5 | `ranked-offers` before `explanation` arrives | Offers are visible and usable while the explanation is still pending | The contract orders it this way deliberately; blocking on the slowest stage discards that |
| E6 | Navigate away mid-stream | The connection closes | Backend task 13 E8 stops work on disconnect; an abandoned search should stop spending budget |
| E7 | API base URL set to a deployed origin | Works cross-origin | The deployed topology. Fails without backend task 19 |
| E8 | A second search after one completes | Its own turn; the previous turn stays intact | A chat log that rewrites its own history is worse than useless |

### Locked decisions

- **The stream drives the turn directly** — no intermediate "search results" store the chat reads from.
  A second model of the same data is a second thing to keep in sync.
- **API base URL from build/runtime configuration**, never a hardcoded host.
- **A supplier that failed is still shown**, not hidden. F06 refines how; that it appears at all is
  decided here — silently dropping it would misrepresent the result set as complete.

## Done when

All eight evals pass, with E2 verified by observation against real timing rather than by a mocked stream
alone.
