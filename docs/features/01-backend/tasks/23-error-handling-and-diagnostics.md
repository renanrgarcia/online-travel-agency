# 23 — Error handling and diagnostics

**Roadmap step:** 8. Safe to expose
**Source doc:** `docs/reference/06-api-sse-contract.md`, `docs/reference/09-lessons-learned.md`
**Depends on:** 13

## Goal

`FlightAi.Api`'s `Program.cs` has no exception-handling middleware at all today. An unhandled
exception anywhere in the pipeline — including `OfflineChatClient`'s own deliberate "unmatched prompt"
throw — reaches the caller as a bare `500` with an empty body, and isn't written anywhere durable
either: nothing shows up without manually turning on App Service filesystem logging after the fact.
Found live on the deployed API while verifying infra task 02 (a demo query missing its diacritic
triggered the unmatched-prompt throw); diagnosing it took a local repro against the published build
because production offered no other way to see what had actually failed.

## Scope

- `UseExceptionHandler` + `AddProblemDetails` (or equivalent) wired into `Program.cs`, so an unhandled
  exception becomes a structured [Problem Details](https://www.rfc-editor.org/rfc/rfc9457) response
  instead of an empty `500`.
- A deliberate decision on where exceptions get logged — App Service filesystem logging (free, but has
  to be declared in Bicep to survive an IaC redeploy, and doesn't survive an F1 instance recycle) versus
  Application Insights (durable, queryable, small ongoing cost, needs its own Bicep resource). Record
  the choice and the reasoning, don't just enable one by habit.
- Explicit behavior for the SSE endpoint specifically, which has two distinct failure shapes: an
  exception before the first event is written (headers not yet sent — a clean Problem Details response
  is possible) versus one after at least one event has streamed (response already committed to
  `text/event-stream` — the status code can't change at that point, only the connection can close).

## Out of scope

- Retrying or recovering from the underlying failure. This task is about making a failure visible and
  diagnosable, not about making more failures survivable.
- The Booking Functions app. Durable Functions has its own status/failure surface
  (`runtimeStatus`/`FailureReason`, `docs/reference/07-booking-saga.md`) already distinct from this
  problem.

## Evals

| ID | Setup | Expected | Why it matters |
|---|---|---|---|
| E1 | An exception thrown before any SSE event is written (e.g. `OfflineChatClient`'s unmatched-prompt case) | Caller receives a structured Problem Details JSON body, not an empty `500` | The actual incident this task is written from |
| E2 | Same exception | It's written to the configured logging destination, with enough detail to identify the cause without a local repro | The whole point — this is what production forensics should look like, not what it looked like today |
| E3 | A normal successful request | Unaffected — all four SSE events stream exactly as task 13's evals define | Exception-handling middleware that touches the happy path is worse than none |
| E4 | An exception thrown mid-stream, after at least one SSE event has already been sent | The connection terminates cleanly (no corrupted partial frame reaches the client) and the exception is still logged server-side, even though the client only observes a dropped stream | Headers are already committed at this point — the fix can't rewrite the status code, so the eval has to prove the middleware doesn't try to and doesn't crash attempting it |
| E5 | The Problem Details response body | Contains no stack trace or internal exception message by default | A diagnosable server side and a safe client-facing response are both required, not a trade-off between them |

### Locked decisions

- **Problem Details for the client-facing shape.** It's the framework-idiomatic answer for Minimal
  APIs, not a bespoke error envelope.
- **Logging destination is a deliberate choice, recorded when this task is implemented** — not
  discovered by whoever debugs the next incident.

## Done when

All five evals pass, and a deliberately triggered unmatched-prompt request against the deployed API
produces both a structured client response and a server-side log entry with no manual log
configuration required beforehand.
