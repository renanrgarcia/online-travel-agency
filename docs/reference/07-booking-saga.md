# FlightAi.Booking.Functions — the booking saga

An Azure Durable Functions orchestration for the booking flow: authorize payment → create order →
issue ticket → send confirmation, each step checkpointed, with compensating actions wired to the first
three steps if a later one fails.

This is the **saga pattern** — a multi-step process broken into local steps, each with a defined
"undo" (a compensating action) if a later step fails, instead of one all-or-nothing transaction —
implemented on top of **Durable Functions**. Durable gives the checkpointing (resume exactly where a
crash left off); saga gives the compensation logic (void the payment, cancel the order) when a later
step fails. Together, "durable saga."

## Why a durable saga

Selecting an offer, creating an order, taking payment, and issuing the ticket are four distinct
operations with four distinct failure modes, and they can fail independently *after* the customer has
already been charged. Each one needs an idempotency key, a durable state machine, and a compensating
action. If the host crashes between any two steps, Durable Functions' checkpointing means it resumes
exactly where it left off on restart — completed activities are never re-executed, only the
orchestrator's control flow replays. This is precisely the workload a stateless prompt loop or a plain
`n8n` workflow cannot safely provide: it needs actual checkpointed state, not a request/response call.

## The saga steps and their compensating actions

| Step | On failure |
|---|---|
| `AuthorizePayment` | Nothing to compensate — it's the first step. |
| `CreateOrder` | Compensate: `VoidPayment`. |
| `IssueTicket` | Compensate, in order: `CancelOrder`, then `VoidPayment`. |
| `SendConfirmation` | Not compensated — the ticket is already real; a failed email doesn't unwind the booking. |

`IssueTicket` failing is the interesting case: closing the "payment authorised, ticket not issued"
failure mode with an explicit compensating action rather than leaving it for a manual ops queue is the
entire point of the saga.

Each activity is wrapped in a retry policy: 3 attempts, first retry after 2 seconds, backoff
coefficient 2.0. Only after all retries are exhausted does the orchestrator treat the step as failed
and begin compensation.

## Idempotency

`bookingId` **is** the orchestration instance ID. A retried or duplicated `POST` with the same
`bookingId` lands on the same saga instance instead of authorizing payment a second time — this is the
entire idempotency mechanism, no separate deduplication table needed.

## Deterministic failure injection

An offer ID containing `FAIL-ORDER` deterministically fails order creation; one containing
`FAIL-TICKET` deterministically fails ticketing. This mirrors the same convention the mock supplier
connectors use (`03-suppliers-and-budget.md`) — failure paths are reproducible on demand for a demo,
not left to chance.

## HTTP contract

**`POST /api/bookings`**

```json
{
  "bookingId": "demo-001", "offerId": "NDC-abc123", "travellerEmail": "t@example.com",
  "amount": 791.00, "currency": "USD", "paymentMethodToken": "tok_test"
}
```

Returns `202 Accepted` with the standard Durable Task check-status payload (`Id`,
`StatusQueryGetUri`, etc.) — the caller polls the status endpoint below.

**`GET /api/bookings/{bookingId}`**

```json
{
  "bookingId": "demo-001",
  "runtimeStatus": "Completed",
  "customStatus": "{\"step\":\"completed\"}",
  "output": "{\"Success\":true,\"AuthorizationId\":\"AUTH-demo-001\",\"OrderId\":\"ORD-demo-001\",\"TicketNumber\":\"TKT-ORD-demo-001\",\"FailedStage\":null,\"FailureReason\":null}",
  "createdAt": "2026-08-19T10:08:47Z",
  "lastUpdatedAt": "2026-08-19T10:08:49Z"
}
```

`customStatus` and `output` are themselves JSON-encoded strings — parse them client-side.
`customStatus` shape: `{ step, stage?, compensated?, warning? }`, where `step` is one of
`authorizing-payment` / `creating-order` / `issuing-ticket` / `sending-confirmation` / `compensating` /
`completed` / `failed`. `output` shape (note: PascalCase — this is Durable Task's default serialization
of the C# `BookingResult` record, not something this API controls):
`{ Success, AuthorizationId, OrderId, TicketNumber, FailedStage, FailureReason }`. On failure, whichever
of `AuthorizationId` / `OrderId` are non-null tells you exactly how far the booking got before it was
rolled back — which is also exactly what was compensated.

## Trying it locally

Needs Azurite (`--skipApiVersionCheck` — see `09-lessons-learned.md`), the Durable Task Scheduler
emulator, and Azure Functions Core Tools. Start the emulator before the Functions host; it listens on
`http://localhost:8080` and its dashboard is available at `http://localhost:8082`.

The emulator connection is configured in `local.settings.json` as
`DURABLE_TASK_SCHEDULER_CONNECTION_STRING` with `TaskHub=default` and `Authentication=None`.
Azurite is still required for the Functions host's own `AzureWebJobsStorage`; it is separate from the
Durable Task Scheduler backend.

```bash
# Happy path
curl -X POST http://localhost:7071/api/bookings -H "Content-Type: application/json" -d '{
  "bookingId": "demo-001", "offerId": "NDC-abc123", "travellerEmail": "t@example.com",
  "amount": 791.00, "currency": "USD", "paymentMethodToken": "tok_test"
}'
curl http://localhost:7071/api/bookings/demo-001   # poll until runtimeStatus is "Completed"

# Compensation path — an offerId containing FAIL-TICKET fails ticketing on purpose
curl -X POST http://localhost:7071/api/bookings -H "Content-Type: application/json" -d '{
  "bookingId": "demo-002", "offerId": "NDC-FAIL-TICKET-xyz", "travellerEmail": "t@example.com",
  "amount": 650.00, "currency": "USD", "paymentMethodToken": "tok_test"
}'
curl http://localhost:7071/api/bookings/demo-002
```
