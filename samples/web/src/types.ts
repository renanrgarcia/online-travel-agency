export interface ParsedIntent {
  origin: string;
  destination: string;
  departureDate: string;
  returnDate: string | null;
  travellers: { adults: number; children: number; infants: number };
  cabin: number;
  preferences: {
    avoidRedEyes: boolean;
    seatPreference: string | null;
    maxStops: number | null;
  };
}

export const CABIN_NAMES = ["Economy", "Premium Economy", "Business", "First"];

export interface SupplierResultEvent {
  supplierId: string;
  succeeded: boolean;
  offerCount: number;
  elapsedMs: number;
  error: string | null;
}

export interface RankedOffer {
  rank: number;
  offerId: string;
  supplierId: string;
  carrier: string;
  price: number;
  currency: string;
  stops: number;
  durationMinutes: number;
  refundable: boolean;
  score: number;
}

export interface Explanation {
  text: string;
  raw: string;
  isClean: boolean;
}

export interface BookingStatus {
  bookingId: string;
  runtimeStatus: "Pending" | "Running" | "Completed" | "Failed" | "Terminated" | string;
  customStatus: string | null;
  output: string | null;
  createdAt: string;
  lastUpdatedAt: string;
}

export interface BookingCustomStatus {
  step: string;
  stage?: string;
  compensated?: string[];
  warning?: string;
}

// Note the PascalCase: this is Durable Task's default serialization of the BookingResult C# record
// (Success, AuthorizationId, ...), not something this app controls — unlike customStatus above, which
// is a hand-written anonymous object serialized as-declared, and unlike the outer envelope in
// BookingStatus, which the API constructs explicitly in camelCase.
export interface BookingOutput {
  Success: boolean;
  AuthorizationId: string | null;
  OrderId: string | null;
  TicketNumber: string | null;
  FailedStage: string | null;
  FailureReason: string | null;
}
