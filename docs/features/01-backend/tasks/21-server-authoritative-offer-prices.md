# 21 — Server-authoritative offer prices

**Roadmap step:** 8. Safe to expose
**Source doc:** `docs/reference/02-price-integrity.md`, `docs/reference/07-booking-saga.md`
**Depends on:** 13, 15
**Build before:** any booking flow is reachable from a browser.

## Goal

Make the price a booking is charged at come from the server, not from the request body.

## The hole this closes

`POST /api/bookings` (task 15) takes `amount` and `currency` from the caller and authorizes payment for
exactly that. Nothing checks them against the offer that was actually searched. A client can post
`"amount": 1.00` for a $730 fare and the saga will happily authorize a dollar.

This is worth stating plainly against the project's own thesis. `docs/reference/02-price-integrity.md`
builds an entire token vocabulary so that **a language model can never author a price the traveller
sees** — and meanwhile **the browser can author the price the traveller pays**. The second hole is
larger than the first and closing it belongs in the same feature, for the same reason.

It's invisible today only because the booking saga has been exercised by `curl` on localhost, where the
caller and the operator are the same person.

## Scope

- The search API issues, per offer, a signed assertion of that offer's authoritative price, currency,
  and expiry.
- `POST /api/bookings` requires it, verifies it, and books against the *verified* values — never the
  client-supplied ones.
- A defined, non-500 rejection for missing, tampered, unknown, or expired assertions.

## Out of scope

- Holding inventory or guaranteeing availability. This binds the *price* to the offer, which is a
  different (and much cheaper) promise than holding a seat.
- Any change to the saga's step sequence or compensation (tasks 15–16).

## Why signing rather than a shared offer store

The API (App Service) and the booking saga (Function App) are separate processes on separate hosts. A
server-side offer table would have to be storage both can reach — a real Azure Storage table, another
moving part to provision, and a second source of truth to expire.

A signed assertion needs neither: the API signs `{offerId, amount, currency, expiry}` with a key both
hosts hold in configuration, and the Function App verifies the signature before trusting any of it. The
data travels through the client without being trustable *from* the client, which is the property that
matters. It's stateless, it's one shared secret rather than one shared database, and it's a pattern
worth having built once.

The trade-off, stated: a signed assertion can't be revoked before its expiry, and a leaked signing key
forges prices silently. Short expiries bound the first; treating the key exactly like the model key
from task 17 (configuration, never source) bounds the second.

## Evals

| ID | Setup | Expected | Why it matters |
|---|---|---|---|
| E1 | Book using an assertion issued by a real search | Succeeds; the authorized amount equals the offer's price as streamed in `ranked-offers` | Baseline — the honest path still works end to end |
| E2 | Book with the `amount` in the body tampered to `1.00`, assertion untouched | Booking is authorized at the **asserted** price, not `1.00` — or rejected outright | The whole task. Either behaviour is defensible; silently charging `1.00` is not |
| E3 | Book with the assertion's payload edited and the signature left alone | Rejected, defined error, no payment authorized | Signature verification actually verifies |
| E4 | Book with a well-formed assertion signed by the wrong key | Rejected | A signature nobody checks the key of is decoration |
| E5 | Book with an assertion past its expiry | Rejected, with a reason distinguishable from "invalid" | An expired price and a forged price are different problems and a caller should be able to tell them apart |
| E6 | Book with no assertion at all | Rejected, defined error, not a `500` | The failure mode a naive or older client hits |
| E7 | The assertion as it appears in the SSE payload | Contains no signing key and nothing not already public to that client | The assertion travels through the browser; it must be safe there |
| E8 | Two searches for the same offer | Assertions differ (distinct expiry), both verify | Determinism of *price*, not of the token — a replayable fixed string would be a worse primitive |

### Locked decisions

- **Assertions are short-lived** — minutes, not hours. Long enough to decide, short enough that a
  leaked one is worthless by the time it's useful.
- **The signing key lives in configuration on both hosts**, never in source — the same rule task 17
  applies to the model key, for the same reason.
- **The server's value always wins.** Where the body and the assertion disagree, the assertion is
  authoritative; the body's price fields become advisory at most.

## Done when

E1–E8 pass. E2 is the one with real money attached, and it's the eval that makes this task's existence
worth it.
