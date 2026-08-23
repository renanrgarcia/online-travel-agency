# 15 — Booking saga orchestrator

**Roadmap step:** 7. Booking saga
**Source doc:** `docs/07-booking-saga.md`
**Depends on:** 14 (Functions project setup)

## Goal

Implement the four booking steps as a Durable Functions orchestration: `AuthorizePayment` →
`CreateOrder` → `IssueTicket` → `SendConfirmation`, each step checkpointed. This task is the happy path
only — no compensation yet, that's task 16.

## Scope

- Four activity functions, one per step, each doing something plausible but mockable (no real payment
  gateway — stub it, consistent with the rest of the system's mock-first approach).
- The orchestrator function calling them in sequence via `CallActivityAsync<T>`.
- A retry policy per activity: 3 attempts, first retry after 2 seconds, backoff coefficient 2.0, per
  `docs/07-booking-saga.md`.
- The `POST /api/bookings` and `GET /api/bookings/{bookingId}` HTTP contract, matching the request/response
  shapes documented in `docs/07-booking-saga.md` exactly (including the `customStatus` /
  `output` JSON-encoded-string shapes — this is an easy place to drift from the documented contract).

## Out of scope (comes later)

- Compensating actions when a step fails — task 16.
- Idempotency via `bookingId` as instance ID — also task 16, though you may find it natural to wire the
  ID mapping in this task; if so, note it, but the *behavior* verification belongs to task 16.

## Done when

- The two curl examples from `docs/07-booking-saga.md`'s "Trying it locally" section work for the happy
  path: POST a booking, poll status, see it reach `Completed` with all four fields
  (`AuthorizationId`, `OrderId`, `TicketNumber`) populated in the output.
- Kill the Functions host mid-orchestration (after step 2, say) and restart it — confirm the
  orchestration resumes from where it left off rather than re-running completed activities. This is the
  concrete proof that "checkpointed" isn't just a word in the doc.
