# 08 — Console demo pipeline

**Roadmap step:** 4. Console demo (first vertical slice)
**Source doc:** none directly — this task is glue code across tasks 01–07
**Depends on:** 01, 02, 03, 04, 05, 06, 07

## Goal

Wire everything built so far into a single console app that runs one search end to end: take a hard-coded
request, fan out to the mock suppliers, rank the results, and print offers with prices rendered through
the token store — no AI, no web server, no Azure Functions yet. This is the first point where you see
the system behave as a system instead of isolated unit-tested pieces.

## Scope

- A console entry point that builds a fixed search request, runs it through
  `SupplierFanOutOrchestrator` (with budget/breaker wired in), scores and ranks the results with
  `OfferScorer`, and prints each offer with its price resolved via `PriceReferenceStore` +
  `ExplanationPlaceholderRenderer` (even though there's no AI-generated prose yet — prove the rendering
  path works with hand-written template strings containing tokens).
- Print enough intermediate state (which connectors responded, budget/breaker status, raw scores) that
  you can see each earlier task's work show up in the final output — this is as much a debugging/learning
  tool as a demo.

## Out of scope (comes later)

- Any AI-generated text — tasks 09–11.
- A web server or SSE — tasks 12–13.

## Done when

- Running the console app produces a ranked list of offers with correctly resolved prices, using only
  the mock connectors.
- You can flip a mock connector to fail (via its failure marker) and rerun, and the console output shows
  graceful degradation — the search still completes with the healthy connector's offers.
- You can point to every line of console output and say which of tasks 01–07 produced it — if you can't,
  the wiring is hiding something worth understanding better before moving on.
