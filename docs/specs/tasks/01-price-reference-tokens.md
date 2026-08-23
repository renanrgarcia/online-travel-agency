# 01 — Price reference tokens

**Roadmap step:** 1. Price integrity core
**Source doc:** `docs/02-price-integrity.md`
**Depends on:** nothing

## Goal

Build `PriceReferenceStore`: given a real price, it hands back an opaque token (something like
`{{PRICE_OFF123}}`) instead of the number itself. Nothing downstream of this store ever sees the digit —
that's the entire point. This is the single most important design decision in the system, so it comes
first.

## Scope

- A store that accepts a price (and whatever identifiers you need — offer ID, currency, amount) and
  returns an opaque token string.
- The store can resolve a token back to its underlying value — but that resolution capability is *only*
  exposed to the specific renderer built in task 02, not to anything that generates text (a model).
- No token format should look like a price at a glance — the point is that a model handling the token
  can't accidentally treat it as a number to reason about or restate.
- Deliberately **no** `MARGIN_`-style token or any token that could leak commercial/margin data — the
  store should only ever mint tokens for values a traveller is allowed to see.

## Out of scope (comes later)

- Rendering the token back into a digit in prose — that's task 02.
- Where prices come from (suppliers) — that's task 04/05.

## Done when

- A unit test proves: given the same price and identifiers, the store returns a token, and the token
  string does not literally contain the numeric price anywhere in it (test this with a regex/substring
  check, not by eye).
- A unit test proves the store can resolve its own token back to the original value.
- A unit test proves two different prices produce different tokens, and the same price/identifier pair
  is stable (produces the same token) — or intentionally not, if you decide tokens are per-request;
  either way, write down which behavior you chose and why, because task 02 and later tasks depend on it.
