import { useState } from "react";
import "./App.css";
import { streamSearch } from "./api";
import { BookingPanel } from "./components/BookingPanel";
import { ExplanationCard } from "./components/ExplanationCard";
import { OfferList } from "./components/OfferList";
import { PipelineStatus } from "./components/PipelineStatus";
import type { Explanation, ParsedIntent, RankedOffer, SupplierResultEvent } from "./types";

const DEFAULT_QUERY = "Two adults, Lisbon to São Paulo, first week of December, no red-eyes, aisle seats";

type Stage = "pending" | "active" | "done";

export default function App() {
  const [query, setQuery] = useState(DEFAULT_QUERY);
  const [searching, setSearching] = useState(false);
  const [intent, setIntent] = useState<ParsedIntent | null>(null);
  const [supplierResults, setSupplierResults] = useState<SupplierResultEvent[]>([]);
  const [offers, setOffers] = useState<RankedOffer[]>([]);
  const [explanation, setExplanation] = useState<Explanation | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [bookingOffer, setBookingOffer] = useState<RankedOffer | null>(null);

  function stageFor(key: "intent" | "suppliers" | "ranking" | "explanation"): Stage {
    if (key === "intent") return intent ? "done" : searching ? "active" : "pending";
    if (key === "suppliers") return offers.length > 0 ? "done" : intent ? "active" : "pending";
    if (key === "ranking") return explanation ? "done" : offers.length > 0 ? "active" : "pending";
    return explanation ? "done" : offers.length > 0 ? "active" : "pending";
  }

  function runSearch() {
    setSearching(true);
    setIntent(null);
    setSupplierResults([]);
    setOffers([]);
    setExplanation(null);
    setError(null);

    streamSearch(query, {
      onIntent: setIntent,
      onSupplierResult: (r) => setSupplierResults((prev) => [...prev, r]),
      onRankedOffers: setOffers,
      onExplanation: setExplanation,
      onDone: () => setSearching(false),
      onError: (message) => {
        setError(message);
        setSearching(false);
      },
    });
  }

  return (
    <div className="app">
      <header className="app-header">
        <h1>Flight AI — live search</h1>
        <p className="subtitle">
          Every stage below is a real call into <code>FlightAi.Core</code> / <code>FlightAi.Agents</code>,
          streamed as it completes — not mocked for this page.
        </p>
      </header>

      <form
        className="search-bar"
        onSubmit={(e) => {
          e.preventDefault();
          if (!searching) runSearch();
        }}
      >
        <input value={query} onChange={(e) => setQuery(e.target.value)} placeholder="Describe the trip…" />
        <button type="submit" disabled={searching}>
          {searching ? "Searching…" : "Search"}
        </button>
      </form>

      {error && <p className="error-text">{error}</p>}

      <PipelineStatus
        stages={{
          intent: stageFor("intent"),
          suppliers: stageFor("suppliers"),
          ranking: stageFor("ranking"),
          explanation: stageFor("explanation"),
        }}
        intent={intent}
        supplierResults={supplierResults}
      />

      <OfferList offers={offers} onBook={setBookingOffer} />

      {explanation && <ExplanationCard explanation={explanation} />}

      {bookingOffer && <BookingPanel offer={bookingOffer} onClose={() => setBookingOffer(null)} />}

      <footer className="app-footer">
        <p>
          Search hits <code>http://localhost:5179</code> (FlightAi.Api). Booking hits{" "}
          <code>http://localhost:7071</code> (FlightAi.Booking.Functions). See the sample README for how
          to start both.
        </p>
      </footer>
    </div>
  );
}
