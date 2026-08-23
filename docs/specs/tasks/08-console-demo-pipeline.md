# 08 — Console demo pipeline

**Roadmap step:** 4. Console demo (first vertical slice)
**Source doc:** glue across tasks 01–07
**Depends on:** 01–07

## Goal

Wire everything so far into one console run: fixed request → supplier fan-out → ranking → offers printed
with prices resolved through the token boundary. No AI, no web server, no Functions yet. First time the
system behaves as a system.

## Scope

- Console entry point running the real pipeline with mock connectors, budget and breaker wired in.
- Prices printed via `PriceReferenceStore` + `ExplanationPlaceholderRenderer`, driven by hand-written
  template strings containing tokens (no model yet).
- Enough intermediate output that each earlier task's contribution is visible.

## Out of scope (comes later)

- AI-generated text — tasks 09–11. HTTP — tasks 12–13.

## Evals

| ID | Scenario | Expected | Why it matters |
|---|---|---|---|
| E1 | Default run | Ranked offer list, every price correctly resolved, zero unresolved tokens in output | The vertical slice works end to end |
| E2 | Run twice | Byte-identical output | Determinism survives integration, not just unit tests |
| E3 | Run with one connector forced to fail | Completes with the healthy connector's offers; the failure is visible in output | Task 06 E2, proven through the full stack |
| E4 | Grep the console output for a raw price digit not produced by the renderer | None found | The token boundary holds when wired for real, not just in task 02's isolated tests |
| E5 | Output inspection | Every stage (intent stub, per-supplier status, scores, final ranking) is individually identifiable | This is a learning tool; opaque output defeats the purpose |
| E6 | Run with a deliberately malformed template containing a raw `$999` | The renderer rejects it and the demo surfaces the violation | Task 02's guard is actually wired in, not bypassed at the integration point |

### Locked decisions

- The demo prints *rendered* text only. Raw model-style text with unresolved tokens never reaches
  stdout except when demonstrating a violation (E6).

## Done when

All six evals pass, and you can point at any line of output and name which task produced it.
