# F10 — Show more offers

**Roadmap step:** 7. Show more offers
**Source doc:** `docs/reference/06-api-sse-contract.md`, `docs/features/01-backend/tasks/26-paginated-search-results.md`
**Depends on:** Backend task 26 (the endpoint and `search-id` event this calls) — blocking, nothing to
build against until it exists. F04 (offer cards — this extends the same list).

## Goal

Let a traveller ask for more than the initial 10 ranked offers, without ever re-triggering a new search
or seeing the list reorder underneath them.

## Scope

- Capture the `search-id` SSE event's `searchId` per assistant turn, alongside the existing stages.
- A "Show more" affordance at the end of the offer list, calling backend task 26's new endpoint with
  the next `offset`/`limit`, appending the results to what's already shown — never replacing or
  reordering the existing list.
- Disabled/hidden once a page comes back with fewer than `limit` offers (nothing more to fetch), or once
  a 404 (`searchId` expired — see backend task 26 E3) makes clear there's nothing left to page into;
  the button's own state is the only affordance for this — no separate error banner needed for "the
  list just ends here."

## Out of scope

- Any redesign of the offer card itself — appended offers use the exact same component F04 already
  built.
- Retrying automatically on a transport failure — matches F06's existing "no automatic retry" locked
  decision.

## Evals

| ID | Setup | Expected | Why it matters |
|---|---|---|---|
| E1 | A search returning a full first page, then "Show more" clicked | The next batch appends below the existing 10, in rank order, without disturbing what's already rendered | The whole point — growing the list, not replacing it |
| E2 | A page whose response has fewer offers than requested | "Show more" becomes unavailable | Reaching the real end of the result set is a normal outcome, not an error state (same spirit as F06 E5's "nothing found") |
| E3 | A `searchId` that's since expired (backend task 26 E3) | A clear, calm message that this search has aged out and a new one is needed — not a raw error dump | Matches F06's existing standard for every other degraded state |
| E4 | Two rapid clicks on "Show more" before the first response lands | Only one request in flight; the second click is a no-op until the first resolves | Same "one thing in flight at a time" discipline F02's composer already enforces for search itself |

### Locked decisions

- **Appends, never replaces.** A traveller who's already started comparing offers should never see
  their reference point shift underneath them.

## Done when

All four evals pass.
