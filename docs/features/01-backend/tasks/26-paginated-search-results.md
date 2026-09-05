# 26 — Paginated search results

**Roadmap step:** 11. Paginated search results
**Source doc:** `docs/reference/06-api-sse-contract.md`, `docs/features/01-backend/tasks/25-duffel-supplier-connector.md`
**Depends on:** 25 (the reason a "show more" is needed at all — the mocks alone never produced enough
offers to make one necessary), 21 (price assertions — a paginated offer needs a freshly issued one, not
a stale one carried over from the original search)

## Goal

Let a traveller see more than the initial 10 ranked offers (task 25's own cap) without re-searching —
and without that re-search silently returning a different set of offers than what they already saw,
since a real supplier's live inventory can shift between calls.

## Scope

- An in-memory cache (`IMemoryCache` — no new package, no new Azure resource; a single App Service
  instance needs nothing more elaborate at this project's scale) storing the *full* ranked offer list a
  search produced, keyed by a freshly generated `searchId`, with a TTL slightly longer than
  `PriceAssertion:ValidityMinutes` (task 25 follow-up widened this to 15) — 20 minutes, so a "show
  more" click near the end of that window can still get a page back with a bookable assertion.
- A new SSE event, `search-id`, carrying `{"searchId": "<guid>"}` — fired once, early (right after
  `parsed-intent`), so a client has it before `ranked-offers` even arrives.
- A new endpoint, `GET /api/search/{searchId}/offers?offset={n}&limit={n}`, returning the same
  `RankedOfferView[]` shape `ranked-offers` already uses, sliced from the cached full list — `rank`
  values continue the original numbering (offset 10 starts at rank 11), never restarting at 1.
- Every offer returned by this endpoint gets a **freshly issued** `PriceAssertion` at request time, from
  the cached price/offerId — never a stale one carried over from the original search. The underlying
  price itself is the one the original search actually found, not re-fetched from any supplier.

## Out of scope

- Any change to how the *first* 10 offers are found or ranked — task 25's own cap stays exactly as is.
- Re-querying suppliers for "more" results. Everything a "show more" click can ever return was already
  found and ranked by the original search; this endpoint only serves slices of it.
- Persisting the cache across an App Service restart/cold start. F1 tier cold-starts after idling
  regardless; a `searchId` from before a restart correctly returns "not found," not stale or wrong data
  — see E3.

## Evals

| ID | Setup | Expected | Why it matters |
|---|---|---|---|
| E1 | A search with more than 10 offers, then `GET .../offers?offset=10&limit=10` against the returned `searchId` | Offers 11–20, in the same rank order the original search found, each `rank` value continuing from 11 — not restarting at 1 | The actual point of this task |
| E2 | The same request repeated twice | Byte-identical offers both times (aside from each `PriceAssertion`'s own fresh signature/expiry) | No hidden re-query — this is what makes "show more" trustworthy instead of just another search |
| E3 | `GET .../offers` against an unknown or expired `searchId` | A clear 404, never a crash or an empty-but-200 response indistinguishable from "no more offers" | A cold-started or long-idle client needs to tell "nothing more to show" apart from "this session is gone, search again" |
| E4 | `offset` beyond the total offers found | An empty array, 200 OK — not an error | Running out of offers is a normal outcome, not a failure |
| E5 | Each offer from `GET .../offers` | Carries a `PriceAssertion` whose `expiresAt` is freshly in the future relative to *this* request, not the original search's timestamp | A traveller who clicks "show more" 10 minutes in must still be able to book what they see |
| E6 | Cache entries over time | Actually evict after the TTL — verified by checking cache state directly, not just trusting `IMemoryCache`'s own documented behavior | This project's own standard: confirm the real behavior, don't assume the library does what its docs say |

### Locked decisions

- **Server-side cache, not re-querying suppliers, and not delegating to a connector-specific pagination
  API.** Duffel's own `List Offers` endpoint does support paging an existing `offer_request_id` —
  considered and rejected here specifically because it only covers Duffel; the mocks have no equivalent,
  and this task wants one uniform mechanism regardless of which connector an offer came from.
- **`IMemoryCache`, not a distributed cache.** A single App Service F1 instance has nothing to
  distribute to; adding Redis or similar here would be exactly the kind of infrastructure this project's
  own free-tier discipline avoids until there's a real reason for it.
- **A new SSE event (`search-id`), not a field bolted onto `ranked-offers`.** `ranked-offers`'s payload
  is a bare array today, already consumed as such by the frontend; wrapping it in an object to carry a
  `searchId` alongside would be a breaking change to an existing, working contract for no necessary
  reason.

## Done when

E1–E6 pass, and a real search against Duffel's test mode can be paged past its first 10 offers with no
second call to Duffel.
