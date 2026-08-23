# 15 — Booking saga orchestrator

**Roadmap step:** 7. Booking saga
**Source doc:** `docs/07-booking-saga.md`
**Depends on:** 14

## Goal

Implement the four booking steps as a Durable orchestration: `AuthorizePayment` → `CreateOrder` →
`IssueTicket` → `SendConfirmation`, each checkpointed. Happy path only — compensation is task 16.

## Scope

- Four activity functions, mocked (no real payment gateway).
- Sequential orchestration via `CallActivityAsync<T>`.
- Retry policy per activity: 3 attempts, first retry 2s, backoff 2.0.
- `POST /api/bookings` and `GET /api/bookings/{bookingId}` matching `docs/07-booking-saga.md` exactly.

## Out of scope (comes later)

- Compensation — task 16.

## Evals

| ID | Setup | Expected | Why it matters |
|---|---|---|---|
| E1 | Happy-path `POST` | `202` then status reaches `Completed` with `AuthorizationId`, `OrderId`, `TicketNumber` all populated | Baseline |
| E2 | `output` field shape | PascalCase `{ Success, AuthorizationId, OrderId, TicketNumber, FailedStage, FailureReason }`, JSON-encoded string | Documented contract; drifting here breaks any client |
| E3 | `customStatus` during the run | Progresses through `authorizing-payment` → `creating-order` → `issuing-ticket` → `sending-confirmation` → `completed` | Callers poll this for progress; wrong values are client-visible |
| E4 | Activity fails transiently twice, succeeds on 3rd | Orchestration completes successfully | Retry policy configured as specified |
| E5 | Activity fails all 3 attempts | Orchestration reports failure with `FailedStage` naming the step | Retries exhaust into a defined failure, not a hang |
| E6 | Kill host after `CreateOrder`, restart | Resumes at `IssueTicket`; `AuthorizePayment` and `CreateOrder` are **not** re-executed | Checkpointing means completed activities never repeat — double-charging is the failure this prevents |
| E7 | Orchestrator code | Contains no `DateTime.Now`, `Guid.NewGuid()`, or direct I/O | Durable replays orchestrator code; non-deterministic calls corrupt replay. Classic first-timer bug |
| E8 | `GET` an unknown `bookingId` | Defined not-found response, not a 500 | — |

### Locked decisions

- Non-determinism stays in **activities**, never the orchestrator (E7). IDs and timestamps are generated
  inside activities and returned.
- Retry policy is uniform across all four activities at this stage.

## Done when

All eight evals pass. E6 and E7 are the ones that teach what Durable actually is.
