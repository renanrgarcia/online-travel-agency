# FlightAi.Api — the search SSE contract

`GET /api/search/stream?q=<natural language query>` — one `EventSource` connection, four Server-Sent
Events streamed in true completion order, not declaration order. Ranked results reach the client before
the explanation prompt has even been sent to a model — that ordering is the whole point: users see
useful results the moment they're ready, and the slower, less critical explanation fills in afterward
rather than blocking everything behind it.

## Events, in order

### `parsed-intent`

Fired once, as soon as `IntentAgentFactory` finishes. Payload is the parsed `SearchRequest`:

```json
{
  "origin": "LIS", "destination": "GRU", "departureDate": "2026-12-01", "returnDate": null,
  "travellers": { "adults": 1, "children": 0, "infants": 0 },
  "cabin": 0,
  "preferences": { "avoidRedEyes": true, "seatPreference": "aisle", "maxStops": null }
}
```

### `supplier-result`

Fired once **per supplier**, in the order each one actually finishes — not the order they were
declared in code. Two suppliers racing in parallel will emit these in whichever order really completes.

```json
{ "supplierId": "ndc-turkish", "succeeded": true, "offerCount": 1, "elapsedMs": 402.0, "error": null }
```

### `ranked-offers`

Fired once, after the fan-out completes and `OfferScorer` finishes. An array, already in ranked order:

```json
[
  { "rank": 1, "offerId": "NDC-...", "supplierId": "ndc-turkish", "carrier": "TK",
    "price": 791.00, "currency": "USD", "stops": 1, "durationMinutes": 1395,
    "refundable": true, "score": 0.628 }
]
```

### `explanation`

Fired once, after `ExplanationAgentFactory` and `ExplanationPlaceholderRenderer` both finish:

```json
{
  "text": "The best match is the TK option at 791.00 USD — 1 stop, 23h 15m total, refundable...",
  "raw": "The best match is the TK option at {{PRICE_NDC-...}} — {{STOPS_NDC-...}}, ...",
  "isClean": true
}
```

`text` is safe to show a user. `raw` is the model's literal output before token resolution — useful for
a debug view showing the tokens in place, exactly as the reference frontend's "show model's raw output"
toggle does. `isClean` is false if any token failed to resolve or a stray digit was found outside a
token — see `02-price-integrity.md`.

### `done` / `error`

`done` closes the stream normally. `error` fires (with a `{ "message": "..." }` payload) if anything in
the pipeline throws, so the client always gets a terminal event rather than a stream that just hangs.

## Why this shape

Each event corresponds to one pipeline stage finishing — intent parsing, supplier fan-out, ranking,
explanation. A client can render a stepper or pipeline visualization that lights up as each stage
lands, and can show the ranked offer list to a user well before the (slower, LLM-bound) explanation
text is ready.
