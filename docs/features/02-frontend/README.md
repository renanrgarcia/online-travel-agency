# Feature 02 — frontend

React + TypeScript + Vite. A chat interface over the backend's streamed search pipeline and booking
saga — the module that turns feature 01 from an API into something a traveller can use.

## Why chat, and why it isn't cosmetic

The backend already streams four distinct events per search (`parsed-intent`, `supplier-result` × N,
`ranked-offers`, `explanation`), each emitted the moment its stage genuinely finishes. Until a client
renders them separately, that design is invisible: `curl` and the test suite are the only observers, and
a plain request/response API would have looked identical from the outside.

A chat interface is the shape that makes per-stage streaming *mean* something. One user message opens
one assistant turn, and that turn fills in progressively as the pipeline runs — the search is confirmed,
suppliers report in one by one, offers appear, and the explanation arrives last. Every event earns its
place because the user watches it land.

It also matches how the system actually thinks. The backend's whole thesis is that AI sits at exactly
two edges — parsing a request in, explaining a result out — with deterministic code in between. A chat
turn is that same sandwich made visible: your words in, a real explanation out, and a ranked list in the
middle that no model chose.

## What it is not

Not an agent, and not a conversational booking assistant that free-associates its way to a purchase.
The user types a search; the backend parses it, decides deterministically, and explains. The chat frame
is a presentation of that pipeline, not a second, looser one layered on top. Anything that looks like
the model making a decision would contradict the feature it's presenting.

## Roadmap

### 1. Foundations

**Tasks:** 01, 02

The Vite project, a typed client for the SSE contract, and a static chat shell. Deliberately separated:
one is protocol correctness with no UI, the other is UI with no network — so neither debugs the other.

### 2. The search turn

**Tasks:** 03, 04

Wiring the four events into a single progressively-rendered assistant turn, then the offer cards and the
comparison view that make a decision possible rather than merely informed.

### 3. The booking turn

**Tasks:** 05

Selecting an offer, starting the saga, and following a long-running orchestration from a chat UI —
including the compensation path, which is the interesting half.

### 4. Honesty and reach

**Tasks:** 06, 07

What the UI does when things degrade — a supplier failed, the explanation came back unclean — and the
bilingual requirement the target market implies.

### 5. Deployment

Azure Static Web Apps, under the same Bicep and CI/CD treatment as the backend — now its own feature,
see [`../03-infra/`](../03-infra/README.md), task 02, originally numbered F08 here.

## Dependencies on feature 01

| Needs | From |
|---|---|
| The four SSE event shapes | `docs/reference/06-api-sse-contract.md` (tasks 12–13) |
| Cross-origin access to the API | Backend task 19 — **blocking** for task 03 here |
| Comparison facts in the explanation | Backend task 18 — shapes task 04 here |
| Booking HTTP contract | `docs/reference/07-booking-saga.md` (tasks 15–16) |
| A price the client can't forge | Backend task 21 — **blocking** for task 05 here |

Tasks 01 and 02 need none of it and can start immediately.
