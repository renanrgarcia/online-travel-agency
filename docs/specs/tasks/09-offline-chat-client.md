# 09 — Offline chat client

**Roadmap step:** 5. AI layer, offline first
**Source doc:** `docs/05-agents-and-intent.md`
**Depends on:** nothing new (independent of the pipeline; needed by tasks 10–11)

## Goal

Build `OfflineChatClient`: a deterministic stand-in for a real model-backed `IChatClient`, so the AI
layer can be built and tested with `dotnet run` and no API key. This is what lets tasks 10 and 11 (and
the whole system, until task 17) run for free and offline.

## Scope

- Implement whatever `IChatClient` interface shape `Microsoft.Agents.AI` / `Microsoft.Extensions.AI`
  expects (check `docs/08-package-versions.md` for the confirmed API surface before guessing).
- Make its responses deterministic and inspectable — e.g. keyed off patterns in the input — so tests in
  tasks 10–11 can assert on specific behavior rather than "it returned something."
- Don't try to make it *smart*. It's a fixture, not a fake LLM — resist the urge to add real NLP logic
  here.

## Out of scope (comes later)

- Anything that reads or writes prices/tokens — the offline client just needs to produce plausible
  structured/text output; task 02's guard is what enforces correctness, not this client.

## Done when

- A unit test proves the offline client implements the expected interface and compiles against the real
  `Microsoft.Agents.AI` / `Microsoft.Extensions.AI` packages (not just an ad hoc interface you invented).
- A unit test proves that a known input pattern produces a known, stable output — this predictability is
  what task 10/11's tests will lean on.
