# F02 — Chat shell

**Roadmap step:** 1. Foundations
**Source doc:** —
**Depends on:** F01 (project only)

## Goal

The conversation surface — message list, composer, turn model — with no network involved. Static data
in, rendered chat out.

## Scope

- A turn model where one user message maps to exactly one assistant turn, and an assistant turn holds
  **stages** that fill in over time rather than a single body of text.
- Message list and composer, with the composer disabled while a turn is in flight.
- Empty state, and a first-run suggestion the demo query actually answers.

## Out of scope

- Any API call — task 03.
- Multi-turn context. Each search is independent: the backend parses one query into one `SearchRequest`
  with no memory of previous ones, and a UI implying otherwise would be lying about what happens.

## Evals

| ID | Setup | Expected | Why it matters |
|---|---|---|---|
| E1 | Submit a message | It appears as a user turn; an assistant turn appears in a pending state | The basic contract of a chat surface |
| E2 | An assistant turn with two of four stages populated | Populated stages render; absent ones don't reserve space or show empty frames | Stages arrive over seconds — the half-filled state is the *normal* state, not an edge case |
| E3 | Composer while a turn is in flight | Disabled, with a visible reason | One in-flight search at a time; concurrent streams would double supplier budget spend for no user benefit |
| E4 | Composer after the turn completes | Re-enabled and focused | The next search shouldn't need a click to start |
| E5 | Empty submission, or whitespace only | Rejected inline, no turn created | The backend's intent agent rejects a blank message outright — better to catch it here than round-trip to an error |
| E6 | Long conversation | Scrolled to the newest content, and not fighting a user who scrolled up | The most common way a streaming chat UI becomes unusable |
| E7 | Keyboard only | Compose, submit, and read a turn without a mouse; assistant turns announced to a screen reader as they update | Content arriving asynchronously is invisible to assistive tech unless it's deliberately announced |
| E8 | First load, no messages | Empty state with a suggestion that produces a real result | The mock connectors answer one query well; a blank box invites queries this system will fail |

### Locked decisions

- **One in-flight search at a time** (E3), for budget reasons above.
- **No conversation persistence.** Reloading starts fresh. Persisting would imply a continuity the
  backend doesn't have.
- **Stages are a fixed, known set**, not an open list — the contract defines exactly four, and modelling
  it loosely would push contract knowledge into rendering code.

## Done when

All eight evals pass against hand-constructed turn data, with no network code in the components.
