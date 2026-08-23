# 14 — Booking Functions project setup

**Roadmap step:** 7. Booking saga
**Source doc:** `docs/07-booking-saga.md`, `docs/09-lessons-learned.md`
**Depends on:** nothing from earlier tasks directly — this is new infrastructure

## Goal

Get an Azure Functions (Durable Task) project running locally against Azurite, with a trivial
orchestration, before writing any real saga logic. Read `docs/09-lessons-learned.md` first — the
Azurite `--skipApiVersionCheck` issue documented there will otherwise cost you real debugging time on
this exact step.

## Scope

- Install Azurite and Azure Functions Core Tools per the versions in `docs/08-package-versions.md`.
- Scaffold the Functions project with the Durable Task extension.
- One trivial orchestration function (e.g. calls a single activity that returns a fixed string) and an
  HTTP trigger that starts it, following `docs/08-package-versions.md`'s confirmed API surface for
  `ScheduleNewOrchestrationInstanceAsync` and `CreateCheckStatusResponseAsync`.

## Out of scope (comes later)

- The real booking saga steps (`AuthorizePayment`, `CreateOrder`, etc.) — task 15.
- Compensation logic — task 16.

## Done when

- `func start` runs locally against Azurite (started with `--skipApiVersionCheck`) with no errors.
- A `curl` POST to your trivial HTTP trigger returns `202 Accepted` with a status-query URL, and polling
  that URL shows the orchestration reach `Completed` with your fixed string as output — this proves the
  whole local dev loop (Azurite + Core Tools + Durable Task) works before any real logic is added on top.
