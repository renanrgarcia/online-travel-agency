# 17 — Swap in a real model

**Roadmap step:** 8. Real model
**Source doc:** `docs/05-agents-and-intent.md`, `docs/08-package-versions.md`
**Depends on:** 09–11 (offline AI layer), 13 (SSE pipeline, so you have somewhere to see real output)

## Goal

Replace `OfflineChatClient` with a real model-backed `IChatClient`, using either Gemini's free tier or
the Microsoft Foundry pattern documented in `docs/05-agents-and-intent.md`. Confirm nothing above
`OfflineChatClient` needed to change — that's the payoff of the interface boundary from task 09.

## Scope

- Pick one path (both are documented and compile-verified):
  - **Gemini free tier**, via the OpenAI-compatible-endpoint pattern in `docs/08-package-versions.md`.
  - **Microsoft Foundry**, via the `Azure.AI.Projects` pattern in `docs/05-agents-and-intent.md`.
- Get an API key (Gemini) or a Foundry project + deployment (Foundry), and wire it into
  `IntentAgentFactory` and `ExplanationAgentFactory` in place of `OfflineChatClient` — via configuration,
  not a code change to either factory.
- Re-run the console demo (task 08) and the SSE pipeline (task 13) against the real model.

## Out of scope

- Nothing further in this roadmap — this is the last task. Optional stretch: try swapping *between*
  Gemini and Foundry (or an Anthropic model hosted in Foundry) to directly confirm the model choice and
  the agent-framework choice really are independent, as `docs/05-agents-and-intent.md` claims.

## Done when

- The console demo (task 08) produces real, non-canned explanation prose, and task 02's structural guard
  still passes — a real model's free-text output going through the trust boundary built in tasks 01–02
  is the real test of whether that boundary actually holds, not just a mock's.
- The SSE pipeline (task 13) streams a real `explanation` event with correctly resolved prices, end to
  end, through a browser or `curl`, with no leaked tokens and no leaked raw prices from the model.
- Neither `IntentAgentFactory` nor `ExplanationAgentFactory`'s own code changed to make this work — only
  what `IChatClient` they were constructed with. If you had to touch the factories, that's a sign task 09
  didn't fully isolate the offline/online seam, worth going back to fix.
