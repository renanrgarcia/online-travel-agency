# 16 — Compensation and idempotency

**Roadmap step:** 7. Booking saga
**Source doc:** `docs/07-booking-saga.md`
**Depends on:** 15

## Goal

Add compensating actions and the idempotency mechanism — what turns a sequence of steps into an actual
saga.

## Scope

- Failure injection: `FAIL-ORDER` fails order creation, `FAIL-TICKET` fails ticketing (same convention
  as task 05).
- Compensation per `docs/07-booking-saga.md`'s table.
- `bookingId` **is** the orchestration instance ID.
- `customStatus` gains `compensated` / `warning` fields.

## Evals

| ID | Setup | Expected | Why it matters |
|---|---|---|---|
| E1 | `FAIL-ORDER` booking | `VoidPayment` called exactly once; `AuthorizationId` populated, `OrderId` null; `FailedStage` = order creation | The simplest compensation path |
| E2 | `FAIL-TICKET` booking | `CancelOrder` then `VoidPayment`, **in that order**, each exactly once | Compensation order is not arbitrary — releasing the order before voting payment mirrors how the booking was built up |
| E3 | `AuthorizePayment` fails | **No** compensation called at all | Nothing to undo; calling `VoidPayment` on a payment that never existed is its own bug |
| E4 | `SendConfirmation` fails | **No** compensation; result still reports overall success with a warning | The ticket is real; a failed email must not unwind a valid booking |
| E5 | Same `bookingId` POSTed twice rapidly | One orchestration instance; `AuthorizePayment` called exactly once | The idempotency guarantee — double-charging is the worst outcome in this system |
| E6 | Same `bookingId` POSTed after the first completed | Returns the existing instance's status, does not restart it | Idempotency holds beyond the concurrent case |
| E7 | Any compensated run's `customStatus` | Shows `compensating` then a terminal state with `compensated` set | Callers can distinguish "failed and rolled back" from "failed, state unknown" |
| E8 | Compensated run's `output` | `AuthorizationId`/`OrderId` populated up to the point reached, `FailedStage` and `FailureReason` set | The output records how far it got, which is exactly what was undone |
| E9 | A compensating activity itself fails | Surfaced explicitly (warning/dead-letter), never silently swallowed | A failed rollback is the worst state in the system — it must be loud |
| E10 | Compensation activities called twice with the same input | Second call is a no-op | Durable retries compensations too; non-idempotent compensation double-refunds |

### Locked decisions

- **Compensation runs in reverse order of completion** (E2).
- **`SendConfirmation` is never compensated** (E4).
- **Compensating activities must themselves be idempotent** (E10) — they run under the same retry
  policy as everything else.

## Done when

All ten evals pass. E5 and E9 are the ones with real money attached.

## Deployment gate

See [`../deployment.md`](../deployment.md), step 3.

| ID | Requirement |
|---|---|
| D1 | `FlightAi.Booking.Functions` deployed to an Azure Functions Consumption plan Function App, backed by a real Azure Storage account — not Azurite |
| D2 | The happy-path and compensation-path curl examples from `docs/07-booking-saga.md` both succeed against the deployed URL |
| D3 | Checkpointing verified against the real runtime, not just Azurite: trigger a booking, and where possible force a transient failure (rather than killing the local host, which isn't available on a managed Function App) to confirm retries and compensation still fire correctly |

This is a materially different deploy from task 13's (Functions vs. App Service), and Durable
Functions' storage account requirements are easy to misconfigure the first time. Ask for a guided
walkthrough here as well.
