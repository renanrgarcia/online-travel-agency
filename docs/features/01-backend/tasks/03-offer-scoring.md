# 03 — Offer scoring

**Roadmap step:** 2. Ranking
**Source doc:** `docs/reference/04-ranking.md`
**Depends on:** nothing

## Goal

Build `ScoringWeights` and `OfferScorer`. Ranking is a deterministic scoring function, not a model
call — this task is where you internalise why: ranking must be explainable, reproducible, and fast,
and a model call guarantees none of the three.

See [`assets/offer-scorer-ranking.svg`](assets/offer-scorer-ranking.svg) for a worked example against
this task's own fixture set — default weights on top, margin-only weights on the bottom, showing why
those two configurations rank the same three offers in opposite orders.

## Scope

- A minimal offer shape sufficient to score (price, duration, stops). Task 04 defines the canonical
  `Offer`; it's fine for this one to be replaced then.
- `ScoringWeights`: named, tunable weights including a `Margin` weight that **defaults to zero**.
- `OfferScorer`: a pure `(offer, weights) -> score`, plus ranking a list by it.

## Out of scope (comes later)

- Where offers come from — tasks 04–07. Score hand-built fixtures here.

## Evals

Fixture set for all evals below — three offers, deliberately with no single dominant winner:

| Offer | Price | Duration | Stops | Margin |
|---|---|---|---|---|
| `CHEAP` | 400 | 11h | 2 | 80 |
| `FAST` | 700 | 6h | 0 | 20 |
| `MID` | 550 | 8h | 1 | 50 |

| ID | Input | Expected | Why it matters |
|---|---|---|---|
| E1 | Rank the fixture set twice with identical weights | Byte-identical ordering both times | Determinism is the property that justifies not using a model |
| E2 | Rank with price weight dominant (others at 0) | `CHEAP`, `MID`, `FAST` | A weight actually drives the outcome in the stated direction |
| E3 | Rank with duration weight dominant (others at 0) | `FAST`, `MID`, `CHEAP` | Same, on an axis that orders the fixtures oppositely to E2 |
| E4 | Rank with **default** weights | Identical ordering whether each offer's margin is its fixture value or `0` | Margin has zero influence unless explicitly weighted — the point of the task |
| E5 | Rank with margin weight explicitly non-zero | Ordering *changes* versus E4 | Proves the lever exists and works, so E4 is a real default rather than a missing feature |
| E6 | Two offers identical on every scored field | A stable, defined order (not exception, not random) | Ties must be deterministic too — decide the tiebreak and pin it |
| E7 | Empty offer list | Empty result, no exception | Degenerate case pinned deliberately |
| E8 | Score a single offer twice with the same weights | Identical score value | Purity: no hidden state, no time-dependence, no culture-dependence |

### Locked decisions

- **`Margin` defaults to `0`.** Margin is a commercial lever, turned on deliberately by whoever
  configures weights, never silently included.
- **Tie-break:** on equal score, order by offer ID ascending. Arbitrary but deterministic, which is
  what matters.
- Scoring uses invariant culture throughout (`docs/reference/09-lessons-learned.md`).

## Done when

All eight evals pass.
