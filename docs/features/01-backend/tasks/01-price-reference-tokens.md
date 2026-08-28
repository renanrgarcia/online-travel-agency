# 01 — Price reference tokens

**Roadmap step:** 1. Price integrity core
**Source doc:** `docs/reference/02-price-integrity.md`
**Depends on:** nothing

## Goal

Build `PriceReferenceStore`: given a real value a traveller may see, it hands back an opaque token
instead of the value itself. Nothing that generates free text ever receives the value — that's the
entire point. This is the single most important design decision in the system, so it comes first.

## Scope

- A store that registers a value against an offer ID and returns an opaque token string.
- The store can resolve a token back to display text — but that capability is exposed only to the
  renderer built in task 02, never to anything that generates text.
- Deliberately **no** margin token and **no** margin registration method. Margin is absent from the
  token vocabulary entirely, which is what makes it unreachable.

## Out of scope (comes later)

- Rendering tokens back into prose — task 02.
- Where offers come from — tasks 04–05.

## Evals

These are the acceptance criteria, fixed **before** implementation. Tests assert exactly these. If the
implementation disagrees with an eval, the implementation is wrong — not the eval.

Token spellings come from `docs/reference/02-price-integrity.md` and are not negotiable. Display formats are
*decided here* (the source doc doesn't specify them), so that tests have an external target rather than
asserting whatever the code happens to produce.

| ID | Input | Expected output | Why it matters |
|---|---|---|---|
| E1 | `RegisterPrice("OFF8812", 791.00m, "USD")` | Returns exactly `{{PRICE_OFF8812}}` | Token spelling is fixed by the source doc; the renderer in task 02 depends on it |
| E2 | The token from E1 | Contains no substring `791`, `791.00`, or `$791` | The opacity property the store exists to provide |
| E3 | `TryResolve("{{PRICE_OFF8812}}")` after E1 | `true`, value `$791.00` | Resolution fidelity — the number survives the round trip intact |
| E4 | `RegisterPrice("OFFA", 500m, "USD")` and `RegisterPrice("OFFB", 500m, "USD")` | Two different tokens | Identical prices must not collapse to one token; offers are addressed individually |
| E5 | `RegisterPrice("OFF1", 100m, "USD")` then `RegisterPrice("OFF1", 999m, "USD")` | Both calls return the **same** token string; resolves to `$999.00` | Token identity is keyed by offer ID alone, never by value — see "Locked decisions" below |
| E6 | `TryResolve("{{PRICE_NEVER_ISSUED}}")` | `false` | Unknown tokens must fail, never resolve to something plausible |
| E7 | `TryResolve("{{MARGIN_OFF8812}}")` | `false` | A hallucinated margin reference must never resolve |
| E8 | Reflect over the store's public API | No public member's name contains `Margin` | Stronger than E7: proves margin is absent *by construction*, not blocked by a check someone could later remove |
| E9 | `RegisterDuration("OFF1", TimeSpan.FromMinutes(330))` | Resolves to `5h 30m` | Display format locked so task 02 and task 11 can rely on it |
| E10 | `RegisterDuration("OFF1", TimeSpan.FromMinutes(120))` | Resolves to `2h` (no `0m`) | Exact-hour case stated explicitly rather than left to implementation |
| E11 | `RegisterStops` with 0, 1, 2 | Resolves to `nonstop`, `1 stop`, `2 stops` | Pluralisation is a correctness detail travellers notice |
| E12 | `RegisterRefundable` with `true`, `false` | Resolves to `refundable`, `non-refundable` | — |
| E13 | `RegisterPriceDelta("OFFA","OFFB", 42.00m, "USD")` | Returns `{{PRICE_DELTA_OFFA_vs_OFFB}}`, resolves to `$42.00 more` | Token spelling fixed by source doc; sign convention locked below |
| E14 | `RegisterPriceDelta("OFFA","OFFB", -15.00m, "USD")` | Resolves to `$15.00 less` | Negative renders as magnitude + direction, never a minus sign in prose |
| E15 | `RegisterPriceDelta("OFFA","OFFB", 0m, "USD")` | Resolves to `the same price` | Zero is a distinct case, not `$0.00 more` |
| E16 | Every register method, called with value `1234.56` | No returned token contains `1234` | Sweep of E2 across the whole API surface |

### Locked decisions

These were genuinely open; deciding them here is what stops tests from being self-referential.

- **Token identity is keyed by offer ID only, never by the value.** If the token's *text* varied with
  the price, two tokens could be compared as strings to infer something about relative prices without
  ever resolving them — which defeats opacity. Consequence: re-registering an offer returns the same
  token and overwrites the resolved value (E5, last write wins).
- **Currency display:** `USD` → `$0.00`, `BRL` → `R$0.00`, `EUR` → `€0.00`, anything else →
  `0.00 CUR`. Always invariant culture, never `CurrentCulture` — see `docs/reference/09-lessons-learned.md`,
  where a `CurrentCulture` assumption is one of the four documented real bugs.
- **Delta sign convention:** the delta argument is B's price minus A's. Positive means B costs more.

## Done when

All sixteen evals pass, `dotnet build` is clean at 0 warnings, and no public API exists through which a
caller could obtain a raw numeric value back out of the store.
