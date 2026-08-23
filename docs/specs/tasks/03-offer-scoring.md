# 03 — Offer scoring

**Roadmap step:** 2. Ranking
**Source doc:** `docs/04-ranking.md`
**Depends on:** nothing (independent of task 01/02, but do it second per the roadmap)

## Goal

Build `ScoringWeights` and `OfferScorer`: ranking offers is a deterministic scoring function over
weighted factors, not a model call. This task is where you internalize why — ranking has to be
explainable, reproducible, and fast, none of which a model call guarantees.

## Scope

- A minimal `Offer` shape sufficient to score (price, duration, stop count — you can stub the rest;
  task 04 defines the real canonical `Offer` model, and it's fine for this task's `Offer` to be replaced
  later).
- `ScoringWeights`: a small set of named, tunable weights (e.g. price weight, duration weight, stops
  weight).
- `OfferScorer`: a pure function `(Offer, ScoringWeights) -> score`, and a way to rank a list of offers
  by that score.
- A `Margin` factor that **defaults to zero**. Don't skip this default — it's the point of the task.
  Margin is a commercial lever; it must be turned on deliberately by whoever configures weights, never
  silently included by default.

## Out of scope (comes later)

- Where offers come from — suppliers (tasks 04–07) come after this, deliberately, so you're scoring
  hand-built test fixtures here, not live data.

## Done when

- A unit test proves that, given a fixed list of offers and fixed weights, ranking is deterministic —
  run it twice, get the identical order both times.
- A unit test proves that changing one weight changes the ranking in the expected direction (e.g.
  increasing the price weight moves a cheaper-but-slower offer up).
- A unit test proves that with default weights, two offers differing only in margin rank identically —
  i.e. margin has zero effect unless explicitly weighted in.
