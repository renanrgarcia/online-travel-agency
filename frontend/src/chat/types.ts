import type { Explanation, ParsedIntent, RankedOffer, SupplierResult } from '../api/contract'
import type { BookingCustomStatus, BookingOutput } from '../api/bookingContract'
import type { Language } from '../i18n/strings'

/**
 * The four stages of one search, as a fixed shape rather than an open list.
 *
 * The SSE contract defines exactly these and no more, so modelling them loosely — a `Map`, an array
 * of `{ name, payload }` — would push contract knowledge out into rendering code and lose the
 * compile-time guarantee that a stage is rendered by something that knows its type.
 *
 * `supplierResults` is a list because the server sends one per connector; the rest are single events.
 */
export interface AssistantStages {
  parsedIntent?: ParsedIntent
  supplierResults: SupplierResult[]
  rankedOffers?: RankedOffer[]
  explanation?: Explanation
}

export type AssistantTurnStatus = 'streaming' | 'complete' | 'failed'

export interface UserTurn {
  id: string
  role: 'user'
  text: string
}

export interface AssistantTurn {
  id: string
  role: 'assistant'
  status: AssistantTurnStatus
  stages: AssistantStages
  /** Set when `status` is `failed` — the reason to show the user. */
  failure?: { message: string }
}

/**
 * `collecting-details` / `submitting` / `polling` are the in-flight states; the rest are terminal.
 * `booked` and `saga-failed` both come from a `runtimeStatus: Completed` response, distinguished by
 * `output.Success` — the saga itself always finishes cleanly, business failure is encoded in its
 * output, never in the orchestration's own runtime status (verified empirically against a real run,
 * see backend task 16's notes). `error` is reserved for what's outside the saga's lifecycle entirely:
 * a rejected POST (missing/invalid price assertion, rate limited), a 404, or a network failure.
 */
export type BookingTurnStatus = 'collecting-details' | 'submitting' | 'polling' | 'booked' | 'saga-failed' | 'error'

export interface BookingTurn {
  id: string
  role: 'booking'
  /** Generated once, when the offer is picked, and never regenerated — including across a duplicate
   * submission of the same attempt (F05 E4). This is the saga's orchestration instance id. */
  bookingId: string
  offer: RankedOffer
  /** Frozen at booking-creation time from the assistant turn the offer was booked from (F07 E3) —
   * a booking turn has no `parsed-intent` of its own to derive it from. */
  language: Language
  status: BookingTurnStatus
  customStatus?: BookingCustomStatus
  output?: BookingOutput
  /** Set when `status` is `error`. */
  error?: { message: string }
}

export type Turn = UserTurn | AssistantTurn | BookingTurn

export function emptyStages(): AssistantStages {
  return { supplierResults: [] }
}
