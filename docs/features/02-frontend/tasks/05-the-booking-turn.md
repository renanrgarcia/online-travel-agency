# F05 — The booking turn

**Roadmap step:** 3. The booking turn
**Source doc:** `docs/reference/07-booking-saga.md`
**Depends on:** F04, backend 15, 16, **21 (server-authoritative prices)**

## Goal

Take a selected offer through the booking saga from inside the chat, and follow a long-running
orchestration honestly — including when it fails and rolls back.

## Scope

- Selecting an offer from F04's cards, collecting the traveller details the contract requires.
- `POST /api/bookings`, then polling `GET /api/bookings/{bookingId}` until a terminal state.
- Rendering saga progress from `customStatus`, and the outcome from `output`.
- Both terminal shapes: booked, and failed-and-compensated.

## Out of scope

- Real payment collection. The saga takes a mock `paymentMethodToken`; a UI that looked like a real card
  form would be a lie about what happens next, and collecting real card data is out of scope for this
  system entirely.
- Retrying a failed booking. `bookingId` is the idempotency key, so a retry is a decision about *which*
  id to use — a real design question this task doesn't answer.

## Evals

| ID | Setup | Expected | Why it matters |
|---|---|---|---|
| E1 | Book a normal offer | `202`, progress renders, terminal state shows the ticket number | Baseline happy path |
| E2 | During the saga | Progress reflects `customStatus` — authorizing payment, creating order, issuing ticket, sending confirmation | Four steps taking seconds each: silence here reads as a hang, and this is the moment a user is most anxious |
| E3 | An offer whose id triggers `FAIL-TICKET` | Failure is shown *and* the rollback is stated explicitly: what was undone | "Something went wrong" after a payment authorization is the worst message this product could send. The saga knows it compensated; the UI must say so |
| E4 | The same booking submitted twice | One booking; the second submission joins the existing one rather than creating a second | `bookingId` is the idempotency key — the UI must not defeat it by generating a fresh id on every click |
| E5 | Terminal failure | `FailedStage` and `FailureReason` surfaced in human terms | The output records exactly how far it got; discarding that leaves a user with no idea what to do next |
| E6 | Polling | Stops at a terminal state and doesn't poll forever | An orphaned poll loop is a battery and quota drain that nothing surfaces |
| E7 | Unknown `bookingId` | The `404` renders as a defined message, not a crash | Backend task 16 E8 defines this response; the client should honour it |
| E8 | The booking request payload | Carries the server-issued price assertion; the client never chooses the amount | Backend task 21's guarantee only holds if the client actually participates in it |

### Locked decisions

- **`bookingId` is generated once per booking attempt** and reused across retries of the same attempt —
  never regenerated on re-submit (E4).
- **Compensation is stated in plain language, not a status code.** The user needs to know their money
  isn't sitting in a void.
- **Polling, not a second SSE stream.** The saga's contract is a status endpoint; inventing a streaming
  transport for it would mean changing the backend to suit a UI preference.

## Done when

All eight evals pass, with E3 exercised against a real `FAIL-TICKET` booking rather than a mocked
response.
