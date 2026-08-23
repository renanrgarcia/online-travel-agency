# 10 — Intent agent

**Roadmap step:** 5. AI layer, offline first
**Source doc:** `docs/05-agents-and-intent.md`
**Depends on:** 09 (offline chat client), 04 (for the `SearchRequest`-adjacent shape, if not already
defined)

## Goal

Build `IntentAgentFactory`: natural language in, a typed, schema-validated `SearchRequest` out. This is
the first of the system's two AI touchpoints, and the one that turns free text into something the rest
of the pipeline (which never reads free text again) can trust.

## Scope

- A `SearchRequest` type capturing whatever the pipeline needs (origin, destination, dates, passenger
  count — scope to what tasks 04–08 actually consume).
- `IntentAgentFactory.Create(IChatClient)` wiring an agent that uses the framework's typed-result call
  (`RunAsync<T>`, per `docs/08-package-versions.md`'s confirmed API surface) to produce a validated
  `SearchRequest`.
- Run it against `OfflineChatClient` from task 09 for now.

## Out of scope (comes later)

- Real model calls — task 17.
- The explanation agent — task 11, separate factory, separate concerns.

## Done when

- A unit test proves that a fixed natural-language input, run through `OfflineChatClient`, produces a
  `SearchRequest` with the expected fields populated.
- A unit test proves malformed/incomplete input is rejected or handled in a defined way (decide and
  document what "handled" means here — this is a real design decision, not a detail) rather than
  producing a garbage `SearchRequest` that silently flows downstream.
