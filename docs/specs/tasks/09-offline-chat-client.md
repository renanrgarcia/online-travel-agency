# 09 — Offline chat client

**Roadmap step:** 5. AI layer, offline first
**Source doc:** `docs/05-agents-and-intent.md`, `docs/08-package-versions.md`
**Depends on:** nothing new

## Goal

Build `OfflineChatClient`: a deterministic stand-in for a real model-backed `IChatClient`, so the AI
layer runs with `dotnet run` and no API key. This is the seam that makes task 17 a config change.

## Scope

- Implement the real `IChatClient` from `Microsoft.Extensions.AI.Abstractions` — the actual interface,
  verified against `docs/08-package-versions.md`, not an invented one.
- Deterministic, pattern-keyed responses so tasks 10–11 can assert specific behaviour.
- A way to make it emit deliberately misbehaving output (raw digits) for task 11's guard tests.

## Out of scope

- Any real intelligence. This is a fixture, not a fake LLM.

## Evals

| ID | Input | Expected | Why it matters |
|---|---|---|---|
| E1 | Compile against real `Microsoft.Extensions.AI` packages | Builds clean; type genuinely implements `IChatClient` | If it only implements a homemade interface, task 17's swap will not work and you won't find out until then |
| E2 | Same prompt twice | Identical response | Determinism — tasks 10/11 assert on exact output |
| E3 | Two different prompts | Different responses | Actually keyed off input, not a constant |
| E4 | Configured "misbehaving" mode | Emits text containing a raw digit outside any token | Supplies task 11 E3's adversarial input from a realistic source |
| E5 | Streaming API surface (if `IChatClient` exposes one) | Implemented, not left throwing `NotImplementedException` | Task 13 streams; a gap here surfaces as a runtime failure two tasks later |
| E6 | Cancellation token, cancelled | Honoured promptly | Consistency with the rest of the system's cancellation discipline |

### Locked decisions

- Responses are keyed by simple substring matching on the prompt. Nothing cleverer — cleverness here
  makes tasks 10–11 harder to reason about, not easier.

## Done when

All six evals pass. E1 is the load-bearing one.
