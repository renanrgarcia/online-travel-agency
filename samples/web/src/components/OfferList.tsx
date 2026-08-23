import type { RankedOffer } from "../types";

interface Props {
  offers: RankedOffer[];
  onBook: (offer: RankedOffer) => void;
}

function formatDuration(minutes: number): string {
  const h = Math.floor(minutes / 60);
  const m = minutes % 60;
  return `${h}h ${m.toString().padStart(2, "0")}m`;
}

export function OfferList({ offers, onBook }: Props) {
  if (offers.length === 0) return null;

  const maxScore = Math.max(...offers.map((o) => o.score));

  return (
    <section className="offers">
      <table>
        <thead>
          <tr>
            <th>#</th>
            <th>Supplier</th>
            <th>Carrier</th>
            <th>Price</th>
            <th>Stops</th>
            <th>Duration</th>
            <th>Fare</th>
            <th>Score</th>
            <th />
          </tr>
        </thead>
        <tbody>
          {offers.map((offer) => (
            <tr key={offer.offerId}>
              <td>{offer.rank}</td>
              <td className="mono">{offer.supplierId}</td>
              <td>{offer.carrier}</td>
              <td className="mono">
                {offer.price.toFixed(2)} {offer.currency}
              </td>
              <td>{offer.stops === 0 ? "nonstop" : `${offer.stops} stop${offer.stops > 1 ? "s" : ""}`}</td>
              <td>{formatDuration(offer.durationMinutes)}</td>
              <td>{offer.refundable ? "refundable" : "non-refundable"}</td>
              <td>
                <div className="score-bar" title={offer.score.toFixed(3)}>
                  <div className="score-bar-fill" style={{ width: `${(offer.score / maxScore) * 100}%` }} />
                </div>
              </td>
              <td>
                <button type="button" onClick={() => onBook(offer)}>
                  Book
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </section>
  );
}
