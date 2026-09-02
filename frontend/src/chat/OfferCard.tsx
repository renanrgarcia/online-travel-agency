import type { RankedOffer } from '../api/contract'
import { useLanguage } from '../i18n/LanguageProvider'
import { formatDuration, formatPrice, formatStops } from './offerFormatting'

/**
 * One ranked offer, every field the payload sent — price, duration, and stops all visible at once
 * without an interaction, since a trade-off hidden behind a click isn't visible at all (F04 E4).
 * `score` is deliberately absent: a real number with no meaning outside the weighting model, useful
 * for a debug view but not for a traveller (F04's locked decision).
 */
export function OfferCard({ offer }: { offer: RankedOffer }) {
  const { strings } = useLanguage()

  return (
    <li className="offer-card">
      <div className="offer-card__rank" aria-label={strings.offerRank}>
        {offer.rank}
      </div>
      <div className="offer-card__body">
        <div className="offer-card__id">{offer.offerId}</div>
        <div className="offer-card__price">{formatPrice(offer.price, offer.currency)}</div>
        <dl className="offer-card__details">
          <div className="offer-card__detail">
            <dt>{strings.offerDuration}</dt>
            <dd>{formatDuration(offer.durationMinutes)}</dd>
          </div>
          <div className="offer-card__detail">
            <dt>{strings.comparisonStops}</dt>
            <dd>{formatStops(offer.stops, strings)}</dd>
          </div>
        </dl>
        <span
          className={
            offer.refundable
              ? 'offer-card__refundable offer-card__refundable--yes'
              : 'offer-card__refundable'
          }
        >
          {offer.refundable ? strings.offerRefundable : strings.offerNonRefundable}
        </span>
      </div>
    </li>
  )
}
