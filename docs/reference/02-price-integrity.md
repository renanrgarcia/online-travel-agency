# Price integrity: the token mechanism

The sharpest rule in this whole system: **a language model must never be the thing that authors a
number a traveller sees.** Prices, durations, stop counts, refund status — none of it should ever pass
through a model's own typed-out text on its way to a user. This document is the mechanism that makes
that true, not just claimed.

## Why prompt discipline isn't enough

Telling a model "never write a number yourself" in a system prompt is not a control — it's a request.
A model can ignore it, misremember it, or get talked out of it by something in its context. The only
way to guarantee a number is correct is to never let the model produce it in the first place, and to
have deterministic code be the only thing allowed to write a digit into the final output.

## The two-sided contract

**Side one — `PriceReferenceStore`** (`FlightAi.Core/Services/Pricing/PriceReferenceStore.cs`). Given a set of
ranked, scored offers, it hands out *tokens*, never numbers: `{{PRICE_OFF8812}}`,
`{{DURATION_OFF8812}}`, `{{STOPS_OFF8812}}`, `{{REFUNDABLE_OFF8812}}`, plus the comparison tokens
`ComparisonFacts` (`FlightAi.Core/Services/Pricing/ComparisonFacts.cs`, see also
`04-ranking.md`) decides are true — `{{PRICE_DELTA_OFFA_vs_OFFB}}`, `{{DURATION_DELTA_OFFA_vs_OFFB}}`,
and superlatives like `{{SUPERLATIVE_CHEAPEST_OFFA}}`. These tokens are what the explanation agent's
prompt is built from — the agent never receives an actual price, duration, stop count, or comparison as
a value it could restate, recompute, or hallucinate a variant of. Resolved text is localized by request
language (English or Brazilian Portuguese); number and date formats stay invariant-culture regardless.

Note what's deliberately absent: **there is no `MARGIN_` token.** Your commercial margin has no
resolvable token at all, so there is no path — intentional or hallucinated — for it to reach a
traveller-facing explanation. This is a design decision, not an oversight, and it's worth preserving
deliberately in any rebuild: the store's token vocabulary is the actual security boundary, and margin
simply isn't in that vocabulary.

**Side two — `ExplanationPlaceholderRenderer`** (`FlightAi.Core/Services/Pricing/ExplanationPlaceholderRenderer.cs`).
This is the *only* code allowed to turn a token into a digit. It takes the model's raw output, finds
every `{{TOKEN}}` pattern, and resolves each one by looking it up in the store — never by trusting
anything the model wrote near the token. A token the store doesn't recognize (including a hallucinated
`MARGIN_` reference, which will never resolve because no such token exists) is never silently dropped:
it's left visibly unresolved, so a bad reference fails loudly instead of quietly leaking whatever text
sat next to it.

## The other half: catching a model that ignores its instructions

A well-behaved model only ever puts numbers inside tokens. But a model *can* ignore that and type
`$999` directly into its prose instead of referencing a token — and if the renderer only checked
well-formed tokens, that raw `$999` would sail straight through untouched. So the renderer also scans
the model's raw text for any digit sitting **outside** a token span at all. A model that writes a
number directly, rather than referencing a token, fails this check even though no token was involved —
that's precisely the failure mode structural enforcement exists to catch, and it's the difference
between a real control and a comment that says "please don't."

## One deliberate exception to the raw-digit scan

An offer's own ID (`LCC-002`, `GDS-001`, ...) is given to the explanation agent as plain text — never a
token, since it isn't a value the traveller needs protected, just a label to write "Offer LCC-002" in
prose. But an offer ID often contains digits, and the raw-digit scan can't otherwise tell "a digit that's
part of an identifier the model was explicitly given" from "a digit the model invented." Found live
against a real model (task 17): the model naturally wrote offer IDs in prose, and every mention tripped
the guard as a false positive. `PriceReferenceStore` now tracks every offer ID it issues a token for
(`KnownOfferIds`), and `ExplanationPlaceholderRenderer` masks those the same way it already masks
`{{TOKEN}}` spans, before scanning for stray digits — so a real invented price still gets caught
alongside a known offer ID, but the ID itself no longer does.

## Read together

Study `PriceReferenceStore.cs`, `ExplanationPlaceholderRenderer.cs`, `ComparisonFacts.cs`, and
`FlightAi.Tests/ExplanationPlaceholderRendererTests.cs` /
`FlightAi.Tests/ComparativeDecisionSupportTests.cs` as one unit — the tests exist specifically to prove
the claims above are actually true of the code, including a test where a mock model deliberately
ignores its instructions and types a raw number, and the renderer catches it anyway.
