# 04 — Supplier connector interface

**Roadmap step:** 3. Suppliers
**Source doc:** `docs/03-suppliers-and-budget.md`
**Depends on:** nothing new (can reuse the `Offer` shape you'll now make canonical)

## Goal

Define the shape every supplier integration talks through: `ISupplierConnector` and the canonical
`Offer` model. This is the seam that lets you add a real supplier later without touching anything above
it in the pipeline.

## Scope

- The canonical `Offer` model — the real one this time, replacing task 03's stub. Include whatever
  fields the rest of the system actually needs (price, currency, duration, stops, carrier, fare rules —
  scope it to what `docs/03-suppliers-and-budget.md` and `docs/04-ranking.md` actually require, not
  everything a real GDS response might contain).
- `ISupplierConnector`: an interface with an async search method that takes a request and returns a list
  of `Offer`s (or a result type that can represent partial failure — decide this deliberately, since task
  06 depends on it).
- No implementation yet — this task is the contract only.

## Out of scope (comes later)

- Actual connector implementations — task 05.
- Calling multiple connectors in parallel — task 06.

## Done when

- The interface compiles and is used nowhere yet except a throwaway test double.
- A short written note (comment or this task file, your choice) on the decision you made for
  representing partial failure in the return type — this decision shapes task 06, so make it consciously
  rather than defaulting to "throws an exception."
