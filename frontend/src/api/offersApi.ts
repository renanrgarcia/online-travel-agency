import type { FetchLike } from './bookingApi'
import type { RankedOffer } from './contract'

/**
 * `GET /api/search/{searchId}/offers` (backend task 26) — a request/response page, not a second SSE
 * stream, so this mirrors {@link import('./bookingApi').createBooking}'s `fetch`-wrapper shape rather
 * than {@link import('./searchStream').openSearchStream}'s.
 */
export type GetMoreOffersResult =
  | { ok: true; offers: RankedOffer[] }
  | { ok: false; expired: true }
  | { ok: false; expired: false; error: string }

export async function getMoreOffers(
  searchId: string,
  offset: number,
  limit: number,
  baseUrl: string,
  fetchImpl: FetchLike = fetch,
): Promise<GetMoreOffersResult> {
  let response: Response
  try {
    response = await fetchImpl(
      `${baseUrl}/api/search/${encodeURIComponent(searchId)}/offers?offset=${offset}&limit=${limit}`,
    )
  } catch {
    return { ok: false, expired: false, error: 'network' }
  }

  // 404 means this searchId is unknown or has aged out of the server's cache (backend task 26 E3) --
  // distinct from a 200 with an empty array, which just means "no more offers" (E4).
  if (response.status === 404) return { ok: false, expired: true }
  if (!response.ok) return { ok: false, expired: false, error: `Unexpected response (${response.status})` }

  const offers = await safeJson<RankedOffer[]>(response)
  if (!offers) return { ok: false, expired: false, error: 'malformed response' }

  return { ok: true, offers }
}

async function safeJson<T>(response: Response): Promise<T | null> {
  try {
    return (await response.json()) as T
  } catch {
    return null
  }
}
