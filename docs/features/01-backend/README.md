# Feature 01 — backend

.NET 10. The deterministic core, the two AI edges, the streaming search API, and the Durable
Functions booking saga.

[`../../reference/`](../../reference/README.md) documents this system in *reading* order:
architecture first, then the invariant that constrains everything else, then outward to suppliers,
ranking, AI, the API, and the booking saga. That's the right order to understand the system. It is not
the right order to *build* it.

This roadmap reorders the same material into *build* order: the pieces with zero external dependencies
first, so each step can be verified with `dotnet test` alone before the next step adds a new kind of
complexity (async fan-out, then AI, then a web server, then Azure Functions). Each step points at the
`reference/0N-*.md` file that is its source of truth, and is broken into individual cards in
[`tasks/`](tasks/README.md).

## 1. Price integrity core

**Source:** `reference/02-price-integrity.md` · **Tasks:** 01, 02

`PriceReferenceStore` + `ExplanationPlaceholderRenderer`. Pure C#, no I/O. This is the one invariant
everything else depends on — a language model must never author a digit the traveller sees — so getting
it right first means every later piece can lean on it instead of re-deriving trust boundaries.

## 2. Ranking

**Source:** `reference/04-ranking.md` · **Tasks:** 03

`OfferScorer` + `ScoringWeights`. Also pure and deterministic, trivially testable. A good second step
because it's conceptually simple but teaches the "why isn't this just a model call" reasoning that
recurs throughout the system.

## 3. Suppliers

**Source:** `reference/03-suppliers-and-budget.md` · **Tasks:** 04, 05, 06, 07

`ISupplierConnector`, mock connectors, `SupplierFanOutOrchestrator`, `LookToBookBudget`,
`SupplierCircuitBreaker`. First taste of async/parallel fan-out and failure handling, still with no real
external dependency since the connectors are mocked.

## 4. AI layer, offline first

**Source:** `reference/05-agents-and-intent.md` · **Tasks:** 09, 10, 11

`IntentAgentFactory`, `ExplanationAgentFactory`, backed by `OfflineChatClient`. Still zero API keys;
validates the agent plumbing against the price-integrity boundary from step 1 before any real model is
involved.

## 5. API + SSE

**Source:** `reference/06-api-sse-contract.md` · **Tasks:** 12, 13

First real ASP.NET Core surface, streaming the pipeline's stages to a client via Server-Sent Events.
The first deployable thing in the whole roadmap.

## 6. Decision support

**Source:** `reference/02-price-integrity.md`, `reference/04-ranking.md` · **Tasks:** 18

Ranking answers *which offer is best*. This step answers *why, compared to what* — deterministic
comparison facts (deltas, superlatives) the explanation agent can state without inventing any of them.
This is where the product stops listing offers and starts helping someone choose, and it extends the
step-1 invariant from "a model may never author a number" to "a model may never author a comparison
either."

## 7. Booking saga

**Source:** `reference/07-booking-saga.md` · **Tasks:** 14, 15, 16, 24

The most infrastructure-heavy step — Azurite, Durable Functions, Azure Functions Core Tools — so it's
worth saving for when the rest of the system is stable and you're not debugging two unfamiliar things at
the same time. Task 24 revisits it later: real billing data showed the default Azure Storage backend's
control-queue polling as a genuine flat daily cost, and Durable Task's own Scheduler backend (paired with
infra task 03) is the fix, not the app-level rate limiting or manual stop/start dials that were the only
options before that was found.

## 8. Safe to expose

**Source:** `../../deployment.md` · **Tasks:** 19, 20, 21, 23

Everything above works on localhost with a trusted caller. This step covers what changes when a browser
on another origin calls it, and when the endpoint is reachable by anyone: CORS, rate limiting, and
server-side price verification at booking time. Task 21 in particular closes a hole the rest of the
system's own thesis implies — the model can't author a price, but until 21 lands, *the browser can*.
Task 23 closes a different gap in the same step: an unhandled exception today reaches the caller as an
empty `500` and isn't logged anywhere durable either, found live while verifying infra task 02.

## 9. Swap in a real model

**Source:** `reference/05-agents-and-intent.md`, `reference/08-package-versions.md` · **Tasks:** 17

Gemini's free tier or the Microsoft Foundry pattern, once the offline path works end to end. Task 17's
20-run stress test is the real payoff of steps 1 and 6 — which is why both should land before it.

## 10. Real supplier integration

**Source:** `reference/12-supplier-api-options.md`, `reference/03-suppliers-and-budget.md` · **Tasks:** 25

The mocks stay forever (same locked decision as the model layer, step 9), but a real supplier alongside
them proves `ISupplierConnector`'s boundary against a dependency this project didn't co-design — real
error shapes, real latency, real staleness, none of which a hand-rolled mock reproduces. Duffel's test
mode is the option that survived validating every free/cheap flight-fare API actually available today
(`reference/12-supplier-api-options.md`): genuinely free, no expiry, no quota, so there's no cost
pressure ever forcing a move to a live token.

## 11. Paginated search results

**Source:** `reference/06-api-sse-contract.md` · **Tasks:** 26

Real supplier data is the reason this step exists — task 25 alone surfaced a single search returning
90+ offers, capped at 10 for the traveller-facing list. A "show more" affordance needs somewhere to page
into without re-querying a supplier and risking a different result set than what was already shown; this
step is that server-side cache and the endpoint over it, not the button itself (frontend task F10).

---

Infrastructure for the Functions app (Bicep + CI/CD, extending what already covers the App Service) is
now its own feature — see [`../03-infra/`](../03-infra/README.md), task 01, originally numbered 22 here.

`reference/09-lessons-learned.md` is worth (re-)reading right before steps 5 and 7 specifically — three
of the four documented bugs live there, and they're cheaper to avoid than to rediscover.

Deployment is covered in [`../../deployment.md`](../../deployment.md) — Azure end to end on free tiers.
Nothing is deployable until step 5, so read it then rather than now.
