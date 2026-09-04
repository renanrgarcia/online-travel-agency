# FlightAi.Api — the search SSE contract

`GET /api/search/stream?q=<natural language query>` — one `EventSource` connection, up to five
Server-Sent Events streamed in true completion order, not declaration order. Ranked results reach the
client before the explanation prompt has even been sent to a model — that ordering is the whole point:
users see useful results the moment they're ready, and the slower, less critical explanation fills in
afterward rather than blocking everything behind it.

These shapes are verified against the running API, not derived from the C# types by inspection —
`frontend/src/api/contract.ts` carries the same note, since this doc previously described an earlier,
never-shipped version of the contract (`travellers`, `cabin`, `preferences`, `supplierId`, `carrier`,
`elapsedMs`, and a `done` terminal event, none of which the server actually sends). Where this doc and
the running server ever disagree again, the server wins and this doc is the thing to correct.

## Events, in order

### `parsed-intent`

Fired once, as soon as `IntentAgentFactory` finishes. Payload is the parsed `SearchRequest`:

```json
{
  "origin": "GRU", "destination": "LIS", "departureDate": "2027-03-12",
  "passengerCount": 2, "language": "en"
}
```

`language` is a BCP-47-ish tag the intent agent inferred from the query's own wording (`en`, `pt-BR`),
not from any header or setting — it's the one field the frontend's chrome ultimately keys off, see
`11-bilingual-ui.md`.

### `supplier-result`

Fired once **per registered connector**, in the order each one actually finishes — not the order they
were declared in code. Connectors racing in parallel emit these in whichever order really completes.

```json
{ "supplierName": "NDC", "status": "Succeeded", "offerCount": 2, "reason": null }
```

`status` is serialized as the C# enum member's own name (a `JsonStringEnumConverter` is registered
specifically so this stays a name, not a number) — one of `Succeeded`, `PartialSuccess`, `Failed`,
`TimedOut`, `Cancelled`, `SkippedCircuitOpen`, `SkippedBudgetExhausted`. The last two are a genuinely
different fact from a call that was made and failed: the orchestrator chose not to call that supplier
at all, per `03-suppliers-and-budget.md`'s budget/breaker. `reason` is populated for anything but
`Succeeded`/`PartialSuccess`.

### `ranked-offers`

Fired once, after the fan-out completes and `OfferScorer` finishes. An array, already in ranked order,
best first:

```json
[
  {
    "rank": 1, "offerId": "LCC-002", "price": 590, "currency": "USD",
    "durationMinutes": 480, "stops": 1, "refundable": false, "score": 1071,
    "priceAssertion": {
      "offerId": "LCC-002", "amount": 590, "currency": "USD",
      "expiresAt": "2026-09-02T17:14:39.097363+00:00",
      "signature": "Y9o7Kq6aCgzTTtA7cIPVFqLyIth+DSOBu2+N+lrOqkA="
    }
  }
]
```

`score` is deliberately never shown to a traveller — see `04-ranking.md`. `priceAssertion` is attached
to every ranked offer, not just the ones the explanation discusses (backend task 21): a signed,
time-boxed proof of that offer's price, opaque to the client, round-tripped verbatim into a booking
request rather than inspected. See `07-booking-saga.md` for how the Booking Functions app verifies it.

### `explanation`

Fired once, after `ExplanationAgentFactory` and `ExplanationPlaceholderRenderer` both finish:

```json
{
  "text": "The best value is $590.00, taking 8h with 1 stop (non-refundable).",
  "raw": "The best value is {{PRICE_LCC-002}}, taking {{DURATION_LCC-002}} with {{STOPS_LCC-002}} ({{REFUNDABLE_LCC-002}}).",
  "isClean": true
}
```

`text` is safe to show a user, and is already written in the query's own detected `language` — the
frontend never re-translates it, see `11-bilingual-ui.md`. `raw` is the model's literal output before
token resolution — useful for a debug view showing the tokens in place, which is exactly what the
frontend's closed-by-default "show raw model output" disclosure does. `isClean` is false if any token
failed to resolve or a stray digit was found outside a token — see `02-price-integrity.md`. When
`isClean` is false, `text` arrives already blanked by the server; the frontend has its own, independent
rule never to fall back to rendering `raw` as prose in that case.

### `error`

The only other event the stream can send, and — along with `explanation` — one of the two ways the
stream ends. Fires with `{ "message": "..." }` if anything in the pipeline throws (a query the intent
agent can't parse, for instance), so the client always gets a defined, actionable terminal event rather
than a stream that just hangs. There is no `done` event: a completed search is told apart from a
dropped connection purely by whether the last event received was `explanation` or `error` — the
frontend tracks this itself (`TERMINAL_EVENT_TYPES` in `contract.ts`) rather than the server saying so
explicitly.

## Why this shape

Each event corresponds to one pipeline stage finishing — intent parsing, supplier fan-out, ranking,
explanation. A client can render a stepper or pipeline visualization that lights up as each stage
lands, and can show the ranked offer list to a user well before the (slower, LLM-bound) explanation
text is ready.
