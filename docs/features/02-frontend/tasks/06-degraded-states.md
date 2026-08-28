# F06 — Degraded states

**Roadmap step:** 4. Honesty and reach
**Source doc:** `docs/reference/02-price-integrity.md`, `docs/reference/03-suppliers-and-budget.md`
**Depends on:** F03, F04

## Goal

Make every partial, failed, or untrusted outcome the backend can produce render as something honest.

The backend degrades deliberately rather than failing: a dead supplier yields partial results, a
misbehaving model yields an explanation flagged unclean, an unparseable query yields an `error` event.
Each of those is a designed outcome with a designed meaning — and all of that design is wasted if the
UI collapses them into one spinner that never resolves.

## Scope

- Every `SupplierStatus`: succeeded, failed, timed out, skipped (budget), skipped (circuit open).
- `isClean: false` on the explanation.
- The `error` event, transport failure, and the zero-offers result.

## Evals

| ID | Setup | Expected | Why it matters |
|---|---|---|---|
| E1 | One supplier failed, others succeeded | Offers render normally; the failure is visible but not alarming | Partial results are the *designed* outcome, not an error state — presenting them as broken teaches users to distrust a working system |
| E2 | Each of the five supplier statuses | Each distinguishable | "Timed out" and "we didn't call them" are genuinely different facts; backend task 07 E8 goes out of its way to keep them distinct |
| E3 | `explanation` with `isClean: false` | The unclean text is **not** shown as prose; the offers stay fully usable | The last line of the price-integrity defence. `text` is deliberately blanked server-side — rendering `raw` here instead would undo tasks 01, 02, and 18 in one component |
| E4 | Same case | The user is told an explanation isn't available, without jargon about tokens or guards | An internal invariant leaking into user-facing copy is its own kind of failure |
| E5 | Zero offers, all suppliers failed | A clear "nothing found", distinguishable from a still-loading state | Backend task 13 emits a deterministic explanation for this; the UI should match its calm |
| E6 | An `error` event | The parse failure is shown with the reason, and the composer re-enables | A rejected query is a normal thing a user can fix by rephrasing |
| E7 | The connection drops mid-stream | Stages already received stay; the interruption is stated; no infinite spinner | F01 disables auto-retry precisely so this is a decision made here rather than a silent re-run |
| E8 | A debug affordance for `raw` | Off by default, opt-in, clearly labelled as the model's unrendered output | The contract includes `raw` for exactly this; making it visible by default would expose the token vocabulary to every user for no benefit |

### Locked decisions

- **`isClean: false` never renders as prose** (E3), in any view, including the debug one — the debug
  view shows `raw` explicitly labelled as raw, which is a different thing from presenting it as an
  answer.
- **A failed supplier is reported, never hidden.** Users deserve to know the result set is partial.
- **No automatic retry anywhere.** Retries against a budgeted, rate-limited backend are the client
  deciding to spend a resource it can't see.

## Done when

All eight evals pass. E3 is the one that matters most — it's where a careless component could quietly
undo the invariant the entire backend is built around.
