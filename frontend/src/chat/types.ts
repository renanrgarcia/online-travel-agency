import type { Explanation, ParsedIntent, RankedOffer, SupplierResult } from '../api/contract'

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

export type Turn = UserTurn | AssistantTurn

export function emptyStages(): AssistantStages {
  return { supplierResults: [] }
}
