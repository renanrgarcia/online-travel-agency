# 02 — Explanation placeholder renderer

**Roadmap step:** 1. Price integrity core
**Source doc:** `docs/02-price-integrity.md`
**Depends on:** 01 (price reference tokens)

## Goal

Build `ExplanationPlaceholderRenderer`: the *only* piece of code allowed to turn a price-reference token
back into a real digit. It takes prose that contains tokens (produced later by a model, in task 11) and
returns prose with the tokens resolved to real numbers.

## Scope

- A renderer that scans a string for token patterns and replaces each with the resolved value from the
  `PriceReferenceStore` built in task 01.
- A structural guard: a check that rejects (or flags) any input string containing a raw digit sequence
  that looks like a price but is **not** inside a recognized token. This is what catches a model that
  ignored its instructions and typed out a number directly — the whole reason this must be enforced in
  code, not by asking a model nicely in a system prompt.
- Handle the case of an unresolvable token (a token the store doesn't recognize) explicitly — decide and
  document what happens (throw? render a placeholder? this is a real design decision, not a detail).

## Out of scope (comes later)

- Actually generating prose with tokens in it — that's task 11 (the explanation agent). For this task,
  write your own test strings by hand.

## Done when

- A unit test proves a string with valid tokens gets those tokens replaced with the correct resolved
  values, and nothing else in the string changes.
- A unit test proves the structural guard catches a hand-crafted "adversarial" input string that contains
  a raw price-looking number outside any token — this is the test that stands in for "what if the model
  ignores instructions."
- A unit test proves the guard does *not* false-positive on ordinary numbers that aren't prices (e.g. a
  flight duration like "2h 30m" or a stop count) — decide what counts as "price-shaped" precisely enough
  to write this test, since an overly broad guard is as much a bug as a missing one.
