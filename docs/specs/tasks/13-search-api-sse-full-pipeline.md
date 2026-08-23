# 13 — Search API, full pipeline

**Roadmap step:** 6. API + SSE
**Source doc:** `docs/06-api-sse-contract.md`
**Depends on:** 12, 08, 10, 11

## Goal

Stream the real pipeline through the SSE endpoint: four event types, emitted as each stage completes
rather than buffered to the end.

## Scope

- `parsed-intent` (task 10), `supplier-result` × N (task 06), `ranked-offers` (task 03), `explanation`
  (task 11, rendered through task 02 before it leaves the server).
- Payload shapes exactly per `docs/06-api-sse-contract.md`.

## Out of scope

- Booking — tasks 14–16. Real model — task 17.

## Evals

| ID | Setup | Expected | Why it matters |
|---|---|---|---|
| E1 | Normal search via `curl -N` | All four event types arrive in documented order | The contract |
| E2 | Connectors with different delays (200ms, 800ms) | `supplier-result` events arrive ≈200ms and ≈800ms in — not together | Per-stage streaming is real, the reason for SSE at all |
| E3 | `explanation` event payload | Contains resolved prices, zero `{{TOKEN}}` strings | Nothing half-rendered reaches a browser |
| E4 | `explanation` payload versus store values | Every price matches the registered value exactly | End-to-end price integrity over HTTP |
| E5 | One connector fails | Its `supplier-result` reports the failure; the stream still completes with `ranked-offers` and `explanation` | Degradation survives the transport layer |
| E6 | Model emits a raw digit (task 09 misbehaving mode) | Server does **not** emit a malformed `explanation`; the violation is handled server-side | The guard protects the client, not just the test suite |
| E7 | Every connector registered | Exactly one `supplier-result` per connector | Task 06 E8 over the wire |
| E8 | Client disconnects after `parsed-intent` | Remaining pipeline work is cancelled | Task 12 E5, with real cost attached |
| E9 | Two identical searches | Identical event sequences and payloads | Determinism end to end, offline model |

### Locked decisions

- **Rendering happens server-side, always.** The browser never receives a token and never learns the
  token vocabulary exists.
- A guard violation (E6) degrades that one event; it does not fail the whole search — offers already
  streamed are still useful.

## Done when

All nine evals pass.

## Deployment gate

Task 13 is the first point in the roadmap where there's something real to deploy. See
[`../deployment.md`](../deployment.md), step 1 (and step 2, once `frontend/` exists to point at it).

| ID | Requirement |
|---|---|
| D1 | `FlightAi.Api` deployed to Azure App Service (Free F1 tier) |
| D2 | `curl -N` against the **deployed** URL — not localhost — shows all four SSE event types arriving in order with per-stage timing preserved (E2's proof, repeated against the real deployment). App Service's proxy layer can buffer streaming responses in ways localhost never does, so this cannot be assumed from local tests passing |
| D3 | `frontend/`, once scaffolded, deployed to Azure Static Web Apps (Free tier) and pointed at the deployed API |

If this is your first time deploying anything to Azure for real rather than running it locally, say so
when you reach this point and ask for a guided, step-by-step walkthrough rather than working from this
table alone — the table is the acceptance bar, not the how-to.
