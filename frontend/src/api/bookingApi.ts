import type { BookingCustomStatus, BookingOutput, BookingStatusResponse, CreateBookingRequest } from './bookingContract'

/**
 * A `fetch` wrapper, not `openSearchStream`'s `EventSourceLike` — this is a request/response API
 * (F05's locked decision: polling, not a second SSE stream), so the test seam is just the global
 * `fetch`, swappable the same way `EventSourceFactory` swaps `EventSource`.
 */
export type FetchLike = typeof fetch

export type CreateBookingResult =
  | { ok: true }
  | { ok: false; error: string; reason?: string }

export async function createBooking(
  request: CreateBookingRequest,
  baseUrl: string,
  fetchImpl: FetchLike = fetch,
): Promise<CreateBookingResult> {
  let response: Response
  try {
    response = await fetchImpl(`${baseUrl}/api/bookings`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    })
  } catch {
    return { ok: false, error: 'network' }
  }

  // 202 is success; anything else (400 missing/invalid assertion, 429, 5xx) is a defined failure to
  // surface, not a thrown exception the caller has to unwrap.
  if (response.status === 202) return { ok: true }

  const body = await safeJson<{ error?: string; reason?: string }>(response)
  return { ok: false, error: body?.error ?? `Unexpected response (${response.status})`, reason: body?.reason }
}

export type BookingStatusResult =
  | { ok: true; status: BookingStatusResponse; customStatus: BookingCustomStatus | null; output: BookingOutput | null }
  | { ok: false; notFound: true }
  | { ok: false; notFound: false; error: string }

export async function getBookingStatus(
  bookingId: string,
  baseUrl: string,
  fetchImpl: FetchLike = fetch,
): Promise<BookingStatusResult> {
  let response: Response
  try {
    response = await fetchImpl(`${baseUrl}/api/bookings/${encodeURIComponent(bookingId)}`)
  } catch {
    return { ok: false, notFound: false, error: 'network' }
  }

  if (response.status === 404) return { ok: false, notFound: true }
  if (!response.ok) return { ok: false, notFound: false, error: `Unexpected response (${response.status})` }

  const status = await safeJson<BookingStatusResponse>(response)
  if (!status) return { ok: false, notFound: false, error: 'malformed response' }

  return {
    ok: true,
    status,
    customStatus: parseJsonField<BookingCustomStatus>(status.customStatus),
    output: parseJsonField<BookingOutput>(status.output),
  }
}

/** `runtimeStatus` values that mean "nothing further will change" — matches Durable Task's own
 * `OrchestrationRuntimeStatus` enum; only `Running` and `Pending` are still in flight. */
export function isTerminalRuntimeStatus(runtimeStatus: string): boolean {
  return runtimeStatus !== 'Running' && runtimeStatus !== 'Pending'
}

function parseJsonField<T>(raw: string | null): T | null {
  if (raw === null) return null
  try {
    return JSON.parse(raw) as T
  } catch {
    return null
  }
}

async function safeJson<T>(response: Response): Promise<T | null> {
  try {
    return (await response.json()) as T
  } catch {
    return null
  }
}
