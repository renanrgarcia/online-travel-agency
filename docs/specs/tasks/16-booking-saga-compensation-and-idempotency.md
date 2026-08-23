# 16 — Compensation and idempotency

**Roadmap step:** 7. Booking saga
**Source doc:** `docs/07-booking-saga.md`
**Depends on:** 15 (booking saga orchestrator)

## Goal

Add the saga's defining feature — compensating actions on failure — and its idempotency mechanism. This
is the task that turns the orchestrator from "a sequence of steps" into an actual saga.

## Scope

- Deterministic failure injection: an offer ID containing `FAIL-ORDER` fails order creation, one
  containing `FAIL-TICKET` fails ticketing, matching the convention from task 05's supplier mocks.
- Compensating actions, exactly per `docs/07-booking-saga.md`'s table:
  - `CreateOrder` failing → compensate with `VoidPayment`.
  - `IssueTicket` failing → compensate with `CancelOrder`, then `VoidPayment`, in that order.
  - `SendConfirmation` failing → **not** compensated (the ticket is already real).
- Idempotency: `bookingId` **is** the orchestration instance ID, so a retried/duplicated `POST` with the
  same `bookingId` lands on the same saga instance instead of double-authorizing payment — no separate
  deduplication table.
- The `customStatus` shape's `compensated` / `warning` fields (per `docs/07-booking-saga.md`) so a caller
  polling status can see that compensation happened, not just that the orchestration ended.

## Out of scope (comes later)

- Nothing — this is the last task in the booking saga step. Task 17 is a different part of the system
  (the AI layer).

## Done when

- The compensation-path curl example from `docs/07-booking-saga.md` (an offer ID containing
  `FAIL-TICKET`) results in a `FailedStage`/`FailureReason` output, with `OrderId` and
  `AuthorizationId` populated (proving they existed) but the booking correctly rolled back — walk through
  the `output` JSON and confirm it tells you exactly how far the booking got and what was undone.
- A test proves that POSTing the same `bookingId` twice in quick succession does not authorize payment
  twice — it lands on the same orchestration instance both times.
- A test proves `AuthorizePayment` failing (the first step) triggers no compensation calls at all, since
  there's nothing to undo yet — confirm your compensation logic doesn't call `VoidPayment` on a payment
  that was never authorized.
