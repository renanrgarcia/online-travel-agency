# 02 — Explanation placeholder renderer

**Roadmap step:** 1. Price integrity core
**Source doc:** `docs/02-price-integrity.md`
**Depends on:** 01 (price reference tokens)

## Goal

Build `ExplanationPlaceholderRenderer`: the *only* code allowed to turn a token into a digit. It takes
a model's raw output, resolves every token through the store from task 01, and — critically — rejects
output where the model wrote a number itself instead of referencing a token.

## Scope

- Resolve every `{{TOKEN}}` in the input by looking it up in `PriceReferenceStore`, never by trusting
  text the model wrote near the token.
- Leave unrecognised tokens **visibly unresolved** rather than silently dropping them, so a bad
  reference fails loudly.
- Scan the raw model text for any digit sitting **outside** a token span, and treat that as a
  violation. This is the check that catches a model ignoring its instructions.

## Out of scope (comes later)

- Generating prose containing tokens — task 11. Hand-write input strings for this task's tests.

## Evals

| ID | Input (raw model text) | Expected | Why it matters |
|---|---|---|---|
| E1 | `"This option is {{PRICE_OFF1}} and takes {{DURATION_OFF1}}."` with OFF1 registered at `791.00 USD` / 330 min | `"This option is $791.00 and takes 5h 30m."` | The happy path: tokens resolve, surrounding words are untouched byte-for-byte |
| E2 | `"Costs {{PRICE_UNKNOWN}}."` | Not a success. The token remains literally present in the output, and the result reports it as unresolved | An unrecognised reference must fail loudly, never vanish leaving plausible-looking prose |
| E3 | `"A great deal at $999."` (no tokens at all) | Rejected as a violation, naming the offending text | The core failure mode: a model that typed a number instead of referencing a token |
| E4 | `"This is {{PRICE_OFF1}}, about 20% cheaper."` | Rejected as a violation — `20` is a digit outside any token | Per `docs/02-price-integrity.md`, **any** digit outside a token span is a violation. A model that computes its own comparison is exactly what this catches |
| E5 | `"Only {{STOPS_OFF1}} and it is {{REFUNDABLE_OFF1}}."` with OFF1 at 0 stops, refundable | `"Only nonstop and it is refundable."` — no violation | Resolved *output* contains no digits here; proves the scan runs on the model's raw input, not on the rendered result |
| E6 | `"It is {{DURATION_OFF1}}."` with OFF1 at 330 min | `"It is 5h 30m."` — no violation, despite the rendered output containing digits | Same point as E5, stated where it's most tempting to get backwards: rendering legitimately *introduces* digits, and that must not trip the guard |
| E7 | `"Try {{MARGIN_OFF1}} today."` | Not a success; reported unresolved (never resolves — task 01 E7/E8) | Margin is unreachable end to end, not merely unregistered |
| E8 | `""` (empty string) | Succeeds, empty output, no violation | Degenerate case pinned so it isn't decided by accident |
| E9 | `"{{PRICE_OFF1}}{{PRICE_OFF2}}"` (adjacent, no separator) | Both resolve correctly | Token boundary detection doesn't depend on surrounding whitespace |
| E10 | A violation result (from E3 or E4) | Carries the specific offending substring, not just a boolean | A failed check has to be diagnosable — "something was wrong" is not actionable when a real model misbehaves |

### Locked decisions

- **The guard's rule is absolute: any digit character outside a token span is a violation.** Not
  "price-shaped numbers" — any digit. This is what `docs/02-price-integrity.md` specifies, and it's
  simpler and stricter than a heuristic. Legitimate numbers (durations, stop counts, prices) reach the
  reader by being *tokens*, so a well-behaved model never needs to type a digit.
- **The scan runs on the model's raw input, before resolution.** Resolution deliberately introduces
  digits into the output; scanning afterward would flag every successful render (E5, E6).
- **Unresolved tokens and digit violations are different outcomes**, not one merged "error". The first
  means the model referenced something that doesn't exist; the second means it bypassed the token
  mechanism. They have different causes and want different handling upstream.

## Done when

All ten evals pass. In particular, E3 and E4 must fail *loudly* — if either passes silently, the entire
price-integrity boundary is decorative.
