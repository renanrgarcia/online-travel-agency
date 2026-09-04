# Frontend architecture — `frontend/`

React + TypeScript + Vite. Not a general-purpose chat client — a presentation layer for one specific
backend pipeline, built so each of that pipeline's real stages (parse, fan out, rank, explain) becomes
something a user actually watches happen, rather than a spinner that resolves into a wall of text. See
`docs/features/02-frontend/README.md` for the fuller case for why chat is the right shape here, not a
cosmetic choice.

## Project structure

```
frontend/src/
  api/            Typed clients for both backends -- the search SSE stream and the booking HTTP
                    contract -- plus the shared payload types (contract.ts, bookingContract.ts).
  chat/            The turn model, every component that renders a turn, and the two hooks that wire
                    chat state to the network (useSearchChat, useBookingFlow).
  i18n/            Strings for both languages and the language-context machinery. See
                    11-bilingual-ui.md.
  config.ts        The two backends' base URLs, read from Vite build-time env, never hardcoded.
```

`api/` and `chat/` mirror the backend's own split between "what the wire says" and "what the app does
with it" — `contract.ts`/`bookingContract.ts` are pure types with no behavior, `searchStream.ts`/
`bookingApi.ts` are the only files that touch a real network primitive (`EventSource`, `fetch`).

## The two backends, two transports

The frontend talks to two genuinely separate Azure resources — `FlightAi.Api` for search,
`FlightAi.Booking.Functions` for booking (see `01-architecture-overview.md`) — and uses a different
transport for each, deliberately:

- **Search** (`api/searchStream.ts`, `openSearchStream`): one `EventSource` per query, dispatching
  `SearchStreamEvent`s as they arrive. Two behaviors here are load-bearing, not incidental:
  - **`EventSource`'s `error` name is overloaded.** A server-sent `event: error` frame (the pipeline's
    own defined failure) and a transport-level connection failure both dispatch to a listener
    registered for `'error'`. They're told apart by shape — the server's arrives as a `MessageEvent`
    carrying `data`, a transport failure as a bare `Event` with none. Getting this wrong would render
    "we couldn't parse your query" as a dropped connection, or vice versa.
  - **Retry is suppressed by closing the connection.** `EventSource` reconnects automatically after a
    transport error, and the standard gives no flag to turn that off. A silent reconnect here would
    re-run the whole search pipeline, including supplier calls that spend the look-to-book budget
    (`03-suppliers-and-budget.md`) — so the first transport failure closes the connection for good
    rather than letting the browser retry underneath the app.
- **Booking** (`api/bookingApi.ts`, `createBooking` + `getBookingStatus`): `POST /api/bookings` once,
  then `GET /api/bookings/{id}` on a plain `setTimeout` loop — not a second SSE stream. The saga's
  status endpoint is request/response, not a push source, and polling is the honest shape for that
  (F05's locked decision). `createBooking`/`getBookingStatus` return a discriminated result type
  (`{ ok: true, ... } | { ok: false, ... }`) rather than throwing, so a rejected POST or an unknown
  `bookingId` is a value the caller has to handle, not an exception it can forget to catch.

Both clients take their transport primitive (`EventSourceFactory`, `FetchLike`) as an injectable
parameter defaulting to the real global — the same test seam shape twice, so every eval in
`docs/features/02-frontend/tasks/` runs with no server at all.

## The chat state model

`chat/useChat.ts` is the single owner of the conversation: one `Turn[]` array, one hook, no second
state container. A `Turn` is a discriminated union:

```ts
type Turn = UserTurn | AssistantTurn | BookingTurn
```

`AssistantTurn` holds `AssistantStages` — `parsedIntent`, `supplierResults[]`, `rankedOffers[]`,
`explanation`, each optional until its own SSE event lands. Rendering an absent stage as nothing at
all (not a placeholder, not a reserved gap) is deliberate: a half-filled turn is the *normal* state
here, not an edge case, since the four stages land seconds apart in production.

`BookingTurn` holds its own `BookingTurnStatus` machine —
`collecting-details → submitting → polling → booked | saga-failed | error`. `booked` and `saga-failed`
both come from the saga reaching `runtimeStatus: Completed`; the orchestration's own status never goes
to a failure state even on business failure (verified empirically against a real run, not assumed —
see `07-booking-saga.md`), so the two are told apart by `output.Success` instead, never by
`runtimeStatus` alone.

`useChat` itself is network-free — every state transition is a plain method (`applyEvent`,
`completeTurn`, `failTurn`, `startBooking`, `updateBooking`, `removeTurn`) that a hook, a test, or a
click handler can call directly. Two separate hooks then join it to the real network:

- **`useSearchChat`** opens `openSearchStream` on `submit`, forwards each event to `applyEvent`, and
  maps a transport failure to `failTurn` with a user-facing message.
- **`useBookingFlow`** exposes `startBooking`/`confirmBooking`, POSTs, and drives the poll loop,
  calling `updateBooking` on every response. It takes `bookingId` and `offer` as explicit parameters
  on every call rather than reading them back off `chat.turns` — a `setTimeout`-chained poll function
  closing over stale React state is exactly the bug class this is designed away, not caught later.

Both hooks operate on the *same* `chat` controller instance, composed together once in `App.tsx`. This
is why a search turn and a booking turn can sit in the same scrolling log, in submission order, with no
separate reconciliation step: there was only ever one array.

## Degraded states are a rendering policy, not an afterthought

Every partial, failed, or untrusted outcome the backend can produce (`06-api-sse-contract.md`,
`02-price-integrity.md`) has a defined rendering, not a shared fallback spinner:

- A failed or skipped `SupplierStatus` is shown with its own translated label, not hidden or collapsed
  into a generic error — partial results are the pipeline's *designed* outcome, not a broken one.
- `explanation.isClean: false` never renders `text` as prose, in any view — `text` is already blanked
  server-side, and falling back to `raw` in its place would undo the price-integrity guarantee in the
  one component positioned to break it. A closed-by-default, explicitly-labelled disclosure is the
  only place `raw` is ever shown.
- Zero ranked offers renders its own "nothing found" message, distinct from a stage that just hasn't
  arrived yet.
- No automatic retry, anywhere. `EventSource`'s auto-reconnect is suppressed (above) and nothing polls
  past a terminal booking state — a client deciding to retry against a budgeted, rate-limited backend
  is spending a resource it can't see the cost of.

## What's deliberately not here

- **No state management library.** One `useState<Turn[]>` plus plain callback methods is the whole
  state layer; nothing here has the fan-out that would justify Redux/Zustand/Jotai.
- **No UI component library, no CSS framework.** Hand-written CSS (`index.css`), styled with CSS
  variables for the two themes' worth of color it actually needs.
- **No i18n library.** See `11-bilingual-ui.md` for why a `Record<Language, Record<Key, string>>`
  lookup table covers everything this UI needs without one.
- **A second SSE stream for booking.** Covered above — polling is the honest shape for a
  request/response status endpoint, not a missing feature.
