# F04 — Offer cards and comparison

**Roadmap step:** 2. The search turn
**Source doc:** `docs/reference/04-ranking.md`, backend task 18
**Depends on:** F03, **backend 18** (for the comparison half)

## Goal

Turn the ranked list into something a person can actually decide from — the offers themselves, and the
comparison between them.

Backend task 18 makes the *explanation* comparative. This task makes the *interface* comparative: seeing
three offers side by side answers "which one" faster than any paragraph, and the paragraph then explains
the trade-off the columns just made visible.

## Scope

- An offer card per ranked offer: price, duration, stops, refundability, rank.
- A comparison affordance across the explained offers — the same dimensions aligned so they can be read
  against each other rather than one at a time.
- Making the trade-off legible: which is cheapest, which is fastest, what the difference costs.

## Out of scope

- Selecting and booking — F05.
- Client-side sorting or filtering. Rank is decided by `OfferScorer` server-side and is explainable and
  reproducible; a client-side re-sort would produce an order nothing on the server can account for.
- Computing any comparison in the browser (see below).

## Evals

| ID | Setup | Expected | Why it matters |
|---|---|---|---|
| E1 | A `ranked-offers` payload of six | Six cards, rank order preserved, every field from the payload rendered | Baseline |
| E2 | Any card | Price rendered exactly as the payload's amount and currency, with no client-side rounding, reformatting, or currency conversion | The invariant this whole system exists to protect does not stop at the API boundary |
| E3 | Comparison view over the explained offers | Price, duration, stops, refundability aligned for direct comparison | The decision the traveller is actually making |
| E4 | An offer that is cheapest but slowest | Both facts visible without expanding anything | The trade-off *is* the product. Hiding half of it behind an interaction defeats the task |
| E5 | The `explanation` text | Displayed as prose alongside the comparison, not instead of it | Two registers of the same answer: the table shows, the prose interprets |
| E6 | Ranked offers arriving before the explanation | Cards and comparison are fully usable while the prose is pending | Consistent with F03 E5 |
| E7 | Exactly one offer returned | Renders sensibly; no comparison affordance shown | A comparison of one is a design bug waiting to ship |
| E8 | The rendered output, inspected | No superlative or comparative claim is computed in the browser — every one traces to server-provided data | See below |

### Locked decisions

- **No comparison is computed client-side** (E8). It would be trivially easy — the payload has every
  number — and it would fork the logic. Backend task 18 makes comparison a deterministic, tested,
  server-side fact precisely so there is one source of truth; recomputing "cheapest" in a component
  creates a second one that can disagree, and the disagreement would surface as a UI that contradicts
  its own explanation paragraph.
- **Rank order is the display order**, always.
- **`score` is not shown to a traveller.** It's a real number with no meaning outside the weighting
  model; showing it invites interpretation it can't support. Useful in a debug view, not in the product.

## Done when

All eight evals pass. E4 and E8 are the ones that matter: E4 because it's the value being delivered, and
E8 because it's the discipline that keeps that value trustworthy.
