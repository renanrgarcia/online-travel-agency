import type { RankedOffer } from '../api/contract'
import { useLanguage } from '../i18n/LanguageProvider'
import { formatDuration, formatPrice, formatStops } from './offerFormatting'

/**
 * Mirrors `SearchPipeline.ExplainedOfferCount` (backend) — the explanation only ever discusses the
 * top 3 ranked offers, and this comparison table is meant to be read alongside that prose (F04 E5),
 * so it covers the same set.
 *
 * There's no field in the contract that actually says "these N were explained" — this is a
 * convention mirrored from a backend constant, not something derived from the payload. If the
 * backend's `ExplainedOfferCount` ever changes, this drifts silently. Worth closing with a real
 * contract field (e.g. an `explainedOfferIds` list) rather than two hardcoded 3s in two codebases.
 */
const COMPARED_OFFER_COUNT = 3

/**
 * A raw side-by-side table — same dimensions as the cards, aligned so they can be read against each
 * other. Deliberately does *not* compute or label anything ("cheapest", "$180 more") — F04's locked
 * decision reserves that for backend task 18's server-computed comparison facts, not yet wired into
 * the contract. Every cell here is a value the payload already sent, laid out differently, not a new
 * fact derived from it.
 */
export function OfferComparison({ offers }: { offers: RankedOffer[] }) {
  const { strings } = useLanguage()
  const compared = offers.slice(0, COMPARED_OFFER_COUNT)

  // A comparison of one offer against nothing isn't a comparison (F04 E7).
  if (compared.length < 2) return null

  return (
    <table className="offer-comparison">
      <caption className="offer-comparison__caption">{strings.comparisonTitle}</caption>
      <thead>
        <tr>
          <th scope="col" className="offer-comparison__row-label" />
          {compared.map((offer) => (
            <th scope="col" key={offer.offerId}>
              #{offer.rank}
            </th>
          ))}
        </tr>
      </thead>
      <tbody>
        <tr>
          <th scope="row">{strings.comparisonPrice}</th>
          {compared.map((offer) => (
            <td key={offer.offerId}>{formatPrice(offer.price, offer.currency)}</td>
          ))}
        </tr>
        <tr>
          <th scope="row">{strings.comparisonDuration}</th>
          {compared.map((offer) => (
            <td key={offer.offerId}>{formatDuration(offer.durationMinutes)}</td>
          ))}
        </tr>
        <tr>
          <th scope="row">{strings.comparisonStops}</th>
          {compared.map((offer) => (
            <td key={offer.offerId}>{formatStops(offer.stops, strings)}</td>
          ))}
        </tr>
        <tr>
          <th scope="row">{strings.comparisonRefundable}</th>
          {compared.map((offer) => (
            <td key={offer.offerId}>
              {offer.refundable ? strings.offerRefundable : strings.offerNonRefundable}
            </td>
          ))}
        </tr>
      </tbody>
    </table>
  )
}
