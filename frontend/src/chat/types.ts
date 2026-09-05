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
/** Matches backend task 25's own cap on `ranked-offers` and task 26's page size for a "show more"
 * request. Whether there's actually anything past that first page is decided by comparing against
 * {@link AssistantStages.totalOffers} (the `offers-total` event), never guessed from this alone --
 * the true count can land exactly on this cap, which a page's length can't distinguish from "more". */
export const OFFERS_PAGE_SIZE = 10

/** Where a "show more" page (F10) stands for this turn. Absent/`'idle'` means there may be more —
 * either `totalOffers` hasn't arrived yet, or it says the shown count is still under it.
 * `'exhausted'` means the shown count has reached `totalOffers` (backend task 26 E4 / the
 * `offers-total` event): a normal end, not an error. `'expired'` means the `searchId` aged out of the
 * server's cache (backend task 26 E3) — a new search is needed, not a retry. */
export type MoreOffersStatus = 'idle' | 'loading' | 'exhausted' | 'expired' | 'error'

export interface AssistantStages {
  parsedIntent?: ParsedIntent
  /** From the `search-id` event (backend task 26) — the key a "show more" page is fetched under. */
  searchId?: string
  supplierResults: SupplierResult[]
  rankedOffers?: RankedOffer[]
  /** From the `offers-total` event (task 26 follow-up) — the true, uncapped offer count. The
   * definitive signal for whether "show more" has anything left to fetch; see {@link MoreOffersStatus}. */
  totalOffers?: number
  explanation?: Explanation
  moreOffersStatus?: MoreOffersStatus
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
