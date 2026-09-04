# F09 — Verify against a real supplier

**Roadmap step:** 6. Real supplier verification
**Source doc:** `docs/reference/12-supplier-api-options.md`, `docs/features/02-frontend/tasks/06-degraded-states.md`
**Depends on:** F06 (degraded states), F04 (offer cards). Backend task 25 (the Duffel connector this
verifies against) — blocking; there's nothing to test until it exists.

## Goal

Confirm the frontend's existing behavior actually holds up against a real, occasionally imperfect
external dependency — not just the mocks' synthetic failure injection. This is deliberately not a new
feature: `supplierName` already renders as a plain, unstyled string with no per-supplier special-casing
(nothing in the frontend currently hardcodes `"GDS"`/`"NDC"`/`"LCC"`), and F06 already built generic
handling for every `SupplierStatus` value. If both of those are genuinely generic, "Duffel" showing up
as a fourth supplier name should need zero new frontend code — this task is where that claim gets
checked against reality instead of just asserted.

## Scope

- Re-run F06's own eval scenarios (partial results, a failed supplier, a timed-out supplier) with
  Duffel's test mode as the failing/slow connector instead of a mock configured to fail on command.
- Confirm `supplierName: "Duffel"` renders correctly wherever a supplier name is shown, with no
  layout, truncation, or hardcoded-list assumption breaking on a name the mocks never exercised.
- If Duffel's test-mode offers carry a currency the mocks never produced, confirm the offer card still
  renders it sensibly (this is a backend `PriceReferenceStore.FormatCurrency` concern primarily, but the
  frontend's own display should be checked too, since it's consuming the already-formatted string).

## Out of scope

- Any new UI for Duffel-specific data the mocks don't have (real airline names, flight numbers, baggage
  allowances) — not planned unless backend task 25 or a later task decides to add such fields to the
  `Offer`/`RankedOfferView` contract. This task verifies existing behavior; it doesn't add new surface.
- Everything backend task 25 itself scopes out (round-trip search, booking against Duffel).

## Evals

| ID | Setup | Expected | Why it matters |
|---|---|---|---|
| E1 | A real search that includes Duffel's connector among the three mocks | All four `supplierName` values render correctly, including "Duffel" — same list styling, no special case needed | Confirms F06 E2's "each of the supplier statuses is distinguishable" claim generalizes to a name the component was never specifically tested against |
| E2 | Duffel's test API is slow enough to hit the configured per-connector timeout (backend task 25 E3) | Renders exactly as F06 E2's "timed out" case already does — the same status, the same visual treatment | The whole point of F06 building this generically: a new supplier shouldn't need new frontend code to degrade correctly |
| E3 | Duffel's test API returns a real error for a malformed request (backend task 25 E2) | Renders exactly as F06's "failed" case — other suppliers' offers stay usable | Same generalization check, for the "failed" status specifically |
| E4 | An offer from Duffel next to offers from the mocks in the same `ranked-offers` event | All render in the same offer-card layout with no visual distinction that isn't already driven by the existing data (price, duration, stops, refundable) | The UI shouldn't need to know or care which connector produced an offer — that's the whole point of the backend's normalized `Offer` type |

### Locked decisions

- **No new frontend code is the success condition, not a shortcut.** If any eval above fails, that's a
  real gap in F06's or F04's genericity to fix — not a sign this task needs to grow new scope.

## Done when

E1–E4 pass. If all four pass with zero frontend code changes, this task's own goal is proven, not merely
attempted.
