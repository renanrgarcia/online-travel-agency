# 11 — Explanation agent

**Roadmap step:** 5. AI layer, offline first
**Source doc:** `docs/05-agents-and-intent.md`, `docs/02-price-integrity.md`
**Depends on:** 09 (offline chat client), 01–02 (tokens + renderer)

## Goal

Build `ExplanationAgentFactory`: prose explaining ranked offers, written from opaque tokens only. This
is where tasks 01–02's trust boundary gets proven end to end — the component that generates text and the
component that resolves numbers are separate, with different trust levels.

## Scope

- `ExplanationAgentFactory.Create(IChatClient)` producing prose referencing task 01's tokens.
- The agent receives tokens only — no offer object carrying a real price reaches it.
- Rendering through task 02's renderer happens *after* the agent returns, outside the agent.

## Out of scope

- Real models — task 17.

## Evals

| ID | Setup | Expected | Why it matters |
|---|---|---|---|
| E1 | Well-behaved offline client, 3 tokenised offers | Prose referencing tokens; after rendering, all resolve, zero unresolved remain | The happy path across the whole boundary |
| E2 | Inspect what the factory passes to the `IChatClient` | Contains no raw price, duration, or stop-count value anywhere in the prompt | The agent *cannot* leak what it was never given — verified on the prompt, not assumed |
| E3 | Offline client in misbehaving mode (task 09 E4), emitting `$999` | Task 02's guard rejects it; the violation reaches the caller | The safety net sits in front of this agent for real, not merely in task 02's isolated tests |
| E4 | Offline client emitting `{{MARGIN_OFF1}}` | Reported unresolved; never resolves | Margin unreachable end to end (task 01 E7/E8, task 02 E7) |
| E5 | Any code path in the factory | No reference to `PriceReferenceStore.TryResolve` | The agent has no *capability* to resolve, structurally — not merely a convention it follows |
| E6 | Same offers twice, offline client | Identical prose | Determinism through the second AI touchpoint |
| E7 | Rendered output | Contains real prices, matching the store's registered values exactly | The numbers a traveller sees came from deterministic code, which is the whole thesis |

### Locked decisions

- **The agent never holds a reference to the store.** It receives a prompt built from tokens; it cannot
  resolve them even if compromised (E5). This is the difference between a boundary and a guideline.
- Rendering happens at the call site *after* the agent returns, never inside the agent.

## Done when

All seven evals pass. E3 and E5 are the load-bearing ones — they're what make this a control rather than
a comment saying "please don't".
