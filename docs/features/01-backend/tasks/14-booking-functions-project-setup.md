# 14 — Booking Functions project setup

**Roadmap step:** 7. Booking saga
**Source doc:** `docs/reference/07-booking-saga.md`, `docs/reference/09-lessons-learned.md`
**Depends on:** nothing from earlier tasks

## Goal

Get an Azure Functions Durable Task project running locally against Azurite with one trivial
orchestration — before any saga logic. Read `docs/reference/09-lessons-learned.md` first; the Azurite API-version
issue documented there will otherwise cost you real debugging time on exactly this step.

## Scope

- Azurite and Core Tools per `docs/reference/08-package-versions.md`.
- Functions project under `backend/src/FlightAi.Booking.Functions` with the Durable Task extension.
- One trivial orchestration and an HTTP trigger starting it.

## Out of scope (comes later)

- Real saga steps — task 15. Compensation — task 16.

## Evals

| ID | Setup | Expected | Why it matters |
|---|---|---|---|
| E1 | `func start` against Azurite | Starts clean, no errors | The local loop works at all |
| E2 | Azurite started **without** `--skipApiVersionCheck` | You observe the documented failure, then fix it with the flag | Deliberately reproduce the known bug once so you recognise it later — a documented lesson you've *seen* beats one you've read |
| E3 | `POST` to the HTTP trigger | `202 Accepted` with a status-query URL | The standard Durable HTTP contract |
| E4 | Poll the status URL | Reaches `Completed` with the fixed string as output | The full round trip |
| E5 | Kill the host mid-orchestration, restart | Orchestration resumes and completes | Checkpointing is real, verified before real logic depends on it |
| E6 | Same instance ID started twice | Second call does not create a second instance | The idempotency primitive task 16 relies on, confirmed at the platform level first |

### Locked decisions

- Azurite always runs with `--skipApiVersionCheck` (after E2 has been observed once deliberately).

## Done when

All six evals pass — the whole local dev loop proven before any booking logic exists.
