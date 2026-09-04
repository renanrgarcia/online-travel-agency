/**
 * The `GET /api/search/stream` Server-Sent Events contract.
 *
 * These shapes were verified against the running API, not transcribed from
 * `docs/reference/06-api-sse-contract.md` — that document predates the rebuild and still describes
 * fields the server never sends (`travellers`, `cabin`, `preferences`, `supplierId`, `carrier`,
 * `elapsedMs`) plus a terminal `done` event that does not exist. Where the two disagree, the server
 * wins and the doc is the thing to correct.
 */

/** The `parsed-intent` payload: the typed `SearchRequest` the intent agent produced. */
export interface ParsedIntent {
  origin: string
  destination: string
  /** ISO date, `YYYY-MM-DD`. */
  departureDate: string
  passengerCount: number
  /** BCP-47-ish tag inferred from the query's own language, e.g. `en`, `pt-BR`. */
  language: string
}

/**
 * Serialized as the enum's C# member name, not camelCase and not a number — the API registers a
 * `JsonStringEnumConverter` specifically so this stays a name.
 */
export type SupplierStatus =
  | 'Succeeded'
  | 'PartialSuccess'
  | 'Failed'
  | 'TimedOut'
  | 'Cancelled'
  | 'SkippedCircuitOpen'
  | 'SkippedBudgetExhausted'

/** The `supplier-result` payload. One per registered connector, in real completion order. */
export interface SupplierResult {
  supplierName: string
  status: SupplierStatus
  offerCount: number
  reason: string | null
}

/**
 * A signed, time-boxed proof that `offerId`'s authoritative price is `amount`/`currency` as of
 * `expiresAt`. Opaque to this client — never inspected, only round-tripped verbatim into the booking
 * request (F05). Safe to hold: it carries nothing beyond what the same search response already showed,
 * and no signing key. See backend task 21.
 */
export interface PriceAssertion {
  offerId: string
  amount: number
  currency: string
  /** ISO 8601 instant. */
  expiresAt: string
  signature: string
}

/** One entry in the `ranked-offers` array. Already in ranked order, best first. */
export interface RankedOffer {
  rank: number
  offerId: string
  price: number
  currency: string
  durationMinutes: number
  stops: number
  refundable: boolean
  /** Lower is better. Deliberately not shown to a traveller — see frontend task F04. */
  score: number
  /** Attached to every offer, not just the explained top few — any ranked offer can be booked. */
  priceAssertion: PriceAssertion
}

/**
 * The `explanation` payload. `text` is safe to show; it is blanked server-side when `isClean` is
 * false. `raw` is the model's output before token resolution, for an opt-in debug view only.
 */
export interface Explanation {
  text: string
  raw: string
  isClean: boolean
}

/** The `error` payload — a pipeline-level failure the server chose to report. */
export interface SearchError {
  message: string
  code?: 'ai-unavailable'
  rawModelResponse?: string | null
}

/**
 * Every event the stream can deliver. Discriminated on `type`, so narrowing on it also narrows
 * `data`, and a `switch` that misses a case fails to compile against {@link assertNeverEvent}.
 */
export type SearchStreamEvent =
  | { type: 'parsed-intent'; data: ParsedIntent }
  | { type: 'supplier-result'; data: SupplierResult }
  | { type: 'ranked-offers'; data: RankedOffer[] }
  | { type: 'explanation'; data: Explanation }
  | { type: 'error'; data: SearchError }

export type SearchStreamEventType = SearchStreamEvent['type']

/** The event names subscribed to. Anything else the server sends is ignored (F01 E8). */
export const SEARCH_STREAM_EVENT_TYPES: readonly SearchStreamEventType[] = [
  'parsed-intent',
  'supplier-result',
  'ranked-offers',
  'explanation',
  'error',
]

/**
 * After one of these, the server has nothing further to send and closing the connection is a normal
 * end of stream rather than an interruption. The contract has no `done` event, so this is how a
 * completed search is told apart from a dropped one.
 */
export const TERMINAL_EVENT_TYPES: readonly SearchStreamEventType[] = ['explanation', 'error']

/** Exhaustiveness helper: a `switch` over `SearchStreamEvent` that misses a case won't compile. */
export function assertNeverEvent(event: never): never {
  throw new Error(`Unhandled search stream event: ${JSON.stringify(event)}`)
}
