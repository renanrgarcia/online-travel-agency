# 11 — Explanation agent

**Roadmap step:** 5. AI layer, offline first
**Source doc:** `docs/05-agents-and-intent.md`
**Depends on:** 09 (offline chat client), 01–02 (price reference tokens + renderer)

## Goal

Build `ExplanationAgentFactory`: the agent that writes prose explaining ranked offers to the traveller —
handed only opaque price tokens from task 01, never a real price, duration, or stop count. This is the
task that proves out the trust boundary from tasks 01–02 end to end: the agent that generates text and
the code that resolves numbers are different components with different trust levels.

## Scope

- `ExplanationAgentFactory.Create(IChatClient)`, producing prose that references offers using the tokens
  from `PriceReferenceStore`, never raw values.
- Feed it offers that already carry price tokens (not real prices) — the agent should have no code path
  that can access a real number.
- Run it against `OfflineChatClient` from task 09, configured to emit tokens in its canned responses so
  you can test the full round trip.

## Out of scope (comes later)

- Real model calls — task 17. (This is the task where a real model's behavior will matter most, since
  it's the one generating free text — worth remembering when you get to task 17.)

## Done when

- A unit test proves the agent's output, run through task 02's `ExplanationPlaceholderRenderer`, resolves
  correctly to real values with no leftover unresolved tokens.
- A unit test proves task 02's structural guard would catch it if the offline client's canned response
  were changed to include a raw digit instead of a token — i.e. confirm the safety net actually sits in
  front of this agent's output, not just in isolation.
