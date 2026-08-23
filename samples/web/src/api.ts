import type {
  BookingStatus,
  Explanation,
  ParsedIntent,
  RankedOffer,
  SupplierResultEvent,
} from "./types";

export const SEARCH_API_BASE = "http://localhost:5179";
export const BOOKING_API_BASE = "http://localhost:7071/api";

export interface SearchStreamHandlers {
  onIntent: (intent: ParsedIntent) => void;
  onSupplierResult: (result: SupplierResultEvent) => void;
  onRankedOffers: (offers: RankedOffer[]) => void;
  onExplanation: (explanation: Explanation) => void;
  onDone: () => void;
  onError: (message: string) => void;
}

/**
 * Consumes GET /api/search/stream — four Server-Sent Events, in this fixed order: parsed-intent,
 * one supplier-result per supplier as it lands, ranked-offers, then explanation. Returns a function
 * that closes the connection.
 */
export function streamSearch(query: string, handlers: SearchStreamHandlers): () => void {
  const url = `${SEARCH_API_BASE}/api/search/stream?q=${encodeURIComponent(query)}`;
  const source = new EventSource(url);

  source.addEventListener("parsed-intent", (e) => handlers.onIntent(JSON.parse((e as MessageEvent).data)));
  source.addEventListener("supplier-result", (e) => handlers.onSupplierResult(JSON.parse((e as MessageEvent).data)));
  source.addEventListener("ranked-offers", (e) => handlers.onRankedOffers(JSON.parse((e as MessageEvent).data)));
  source.addEventListener("explanation", (e) => handlers.onExplanation(JSON.parse((e as MessageEvent).data)));
  source.addEventListener("error", (e) => {
    const data = (e as MessageEvent).data;
    handlers.onError(data ? JSON.parse(data).message : "Connection lost.");
    source.close();
  });
  source.addEventListener("done", () => {
    handlers.onDone();
    source.close();
  });

  return () => source.close();
}

export interface StartBookingRequest {
  bookingId: string;
  offerId: string;
  travellerEmail: string;
  amount: number;
  currency: string;
  paymentMethodToken: string;
}

export async function startBooking(request: StartBookingRequest): Promise<void> {
  const response = await fetch(`${BOOKING_API_BASE}/bookings`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request),
  });
  if (!response.ok) {
    throw new Error(`Failed to start booking (HTTP ${response.status}).`);
  }
}

export async function getBookingStatus(bookingId: string): Promise<BookingStatus> {
  const response = await fetch(`${BOOKING_API_BASE}/bookings/${encodeURIComponent(bookingId)}`);
  if (!response.ok) {
    throw new Error(`Failed to fetch booking status (HTTP ${response.status}).`);
  }
  return response.json();
}
