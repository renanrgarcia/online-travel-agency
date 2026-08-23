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

## Why the guard is a deterministic scanner, not a second AI call

The obvious-sounding alternative is an LLM-as-judge: a second model call that reviews the first model's
output and says whether it's safe. Rejected, deliberately, for this specific job:

- **It would contradict the reason this task exists.** `docs/02-price-integrity.md`'s whole argument is
  that a model's compliance can never be trusted as the enforcement mechanism — only deterministic code
  can be. An LLM reviewer is still a model. It can be fooled, hallucinate a "looks fine" verdict, or
  simply have a false-negative rate above zero. You haven't removed the trust problem, you've moved it
  one layer and made it harder to see.
- **It's provable versus probabilistic, for a claim that's provable.** "Does this string contain a digit
  character outside a token span" has an exact, cheap, 100%-reliable answer via a regex scan. Spending a
  second model call — with its own non-zero failure rate — to approximate an answer you can compute
  exactly is strictly worse on every axis for this specific claim.
- **Cost and latency, concretely.** A reviewer call doubles AI usage per explanation. Against Gemini's
  free tier (task 17, ~10 requests/minute), that halves your effective throughput for no gain on the one
  guarantee that actually matters. It also sits in the SSE pipeline (task 13) as a second sequential
  model call before the `explanation` event can be emitted, working against the whole point of streaming
  per-stage results quickly.

An LLM reviewer is legitimate for a *different* class of question — "does this read naturally," "is the
tone on-brand," "does it stay on-topic" — where there's no exact answer to compute and probabilistic
judgment is the right tool. That's a quality check, optional, and it must never be the thing standing
between a model and price integrity. If you want one later, it goes in *addition to* this scanner, never
in place of it.

What the scanner in this task **can't** catch, honestly stated: a model that spells a number out in
words instead of digits — `"seven hundred ninety-one dollars"` has no digit character at all. Evals E11
and E12 close part of that gap deterministically (a fixed word list, not a model), with the remaining
gap stated explicitly in Locked decisions below rather than hidden.

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
| E11 | `"It costs about seven hundred ninety-one dollars for the trip."` (no digits, no tokens) | Rejected as a violation, naming the offending phrase | Closes the digit-scan's blind spot for a model that spells the number out in English |
| E12 | `"Custa cerca de setecentos reais para a viagem."` (Portuguese, no digits, no tokens) | Rejected as a violation | The target market is Brazilian (see `docs/03-suppliers-and-budget.md`); an English-only guard would be a product bug, not just an incomplete one |

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
- **The word-number scan only checks magnitude words** — `hundred`/`thousand`/`million`/`billion` and
  their Portuguese equivalents (`cem`, `mil`, `milhão`, `bilhão`, …) — never the numbers one to twenty
  spelled out (`one`, `two`, `um`, `dois`, …). Those words are common pronouns and adjectives in both
  languages ("the only **one**", "**um** momento") and would produce an unacceptable false-positive
  rate if flagged. **This is a disclosed, real gap**: a model that writes "five stops" instead of using
  a `STOPS` token slips through. Closing it fully would need either a much more careful
  language-specific parser or accepting the LLM-reviewer cost/reliability trade-off argued against
  above — worth revisiting if it's ever observed in practice, not worth solving speculatively now.

## Done when

All twelve evals pass. In particular, E3, E4, E11, and E12 must fail *loudly* — if any of them pass
silently, the price-integrity boundary is decorative for that failure mode.
