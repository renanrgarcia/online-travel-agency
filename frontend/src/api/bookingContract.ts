import type { PriceAssertion } from './contract'

/**
 * The booking saga's HTTP contract (`docs/reference/07-booking-saga.md`) — a different host from the
 * search API (Azure Functions vs. App Service), reached with plain `fetch`, not SSE: this is a
 * request/response API with a poll, not a stream (F05's locked decision).
 */

/** The `POST /api/bookings` body. `priceAssertion` is round-tripped verbatim from the offer the
 * traveller picked — this client never computes or edits it (backend task 21's guarantee only holds
 * if the client actually participates). `amount`/`currency` mirror the same offer for a human reading
 * the request, but the server always prefers the assertion's values over these. */
export interface CreateBookingRequest {
  bookingId: string
  offerId: string
  travellerEmail: string
  amount: number
  currency: string
  paymentMethodToken: string
  priceAssertion: PriceAssertion
}

/** The `GET /api/bookings/{bookingId}` envelope. `customStatus` and `output` are themselves
 * JSON-encoded strings — parsed separately, not by this shape. */
export interface BookingStatusResponse {
  bookingId: string
  runtimeStatus: string
  customStatus: string | null
  output: string | null
  createdAt: string
  lastUpdatedAt: string
}

/** Parsed `customStatus`. `step` mirrors the saga's own stage names exactly (kebab-case, server
 * chosen) — translated to human language only at render time, never stored translated. */
export type BookingStep =
  | 'authorizing-payment'
  | 'creating-order'
  | 'issuing-ticket'
  | 'sending-confirmation'
  | 'compensating'
  | 'completed'
  | 'failed'

export interface BookingCustomStatus {
  step: BookingStep
  stage?: string
  compensated?: boolean
  warning?: string
}

/**
 * Parsed `output` — PascalCase is intentional, not a bug to normalize: this is Durable Task's own
 * default serialization of the C# `BookingResult` record, passed through exactly as the server sent
 * it (`docs/reference/07-booking-saga.md`).
 */
export interface BookingOutput {
  Success: boolean
  AuthorizationId: string | null
  OrderId: string | null
  TicketNumber: string | null
  FailedStage: string | null
  FailureReason: string | null
}
