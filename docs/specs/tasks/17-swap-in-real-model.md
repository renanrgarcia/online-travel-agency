# 17 — Swap in a real model

**Roadmap step:** 8. Real model
**Source doc:** `docs/05-agents-and-intent.md`, `docs/08-package-versions.md`, `docs/specs/deployment.md`
**Depends on:** 09–11, 13

## Goal

Replace `OfflineChatClient` with a real model-backed `IChatClient` and confirm nothing above it needed
to change. That's the payoff of task 09's boundary — and the first time the price-integrity guard faces
genuinely unpredictable text.

## Scope

- Gemini free tier via the OpenAI-compatible endpoint pattern (`docs/08-package-versions.md`), or
  Microsoft Foundry via `Azure.AI.Projects` (`docs/05-agents-and-intent.md`). See
  `docs/specs/deployment.md` for why Gemini is the free choice and Foundry the production one.
- Wire it in by **configuration**, not by editing either agent factory.

## Evals

| ID | Setup | Expected | Why it matters |
|---|---|---|---|
| E1 | `git diff` on `IntentAgentFactory` and `ExplanationAgentFactory` after the swap | No changes | If either needed editing, task 09's seam leaked and is worth fixing before continuing |
| E2 | Console demo with the real model | Real prose, all prices resolved, task 02's guard passes | The boundary holds against real model output, not just a fixture's |
| E3 | Run the same search 20 times | Every run either renders cleanly or is caught by the guard; **no run leaks a raw number to output** | The real test of tasks 01–02. A model *will* eventually type a digit; the system must stay correct when it does |
| E4 | Any run where the guard fires | Logged with the offending text | You need to *see* the failure mode you designed for actually occur |
| E5 | Intent parsing, Portuguese input | Correct `SearchRequest` (task 10 E7 against a real model) | The target market |
| E6 | SSE endpoint (task 13) with the real model | `explanation` event streams correctly, no tokens leak | End to end over the real transport |
| E7 | API key handling | Never in source, never in the SSE payload, never reaching the browser | The model is called server-side only |
| E8 | Swap Gemini ↔ Foundry (stretch) | Both work; only configuration differs | Proves the model choice and framework choice are genuinely independent |

### Locked decisions

- The key lives in configuration/environment, never in the repository.
- The offline client stays in the codebase permanently — tests keep running against it, so the suite
  remains free, fast, and deterministic.

## Done when

E1–E7 pass. E3 is the one that retroactively justifies tasks 01 and 02 having come first.

## Deployment gate

See [`../deployment.md`](../deployment.md), step 4.

| ID | Requirement |
|---|---|
| D1 | The model API key lives in App Service Configuration (or Key Vault) — never in source control, never shipped to the browser |
| D2 | The **deployed** `FlightAi.Api` (not local) streams a real-model explanation end to end, per E6 above |
| D3 | Rotate the key once and redeploy without a code change — confirms the key is read from configuration at runtime, not baked into a build |

This is the deployment step with the most consequence if done wrong — a leaked key is billable to you
directly. Ask for a guided walkthrough and don't skip D3.
