import { useLanguage } from '../i18n/LanguageProvider'
import type { AssistantStages, AssistantTurn } from './types'
import type { Strings } from '../i18n/strings'
import type { RankedOffer } from '../api/contract'
import { OfferCard } from './OfferCard'
import { OfferComparison } from './OfferComparison'

/**
 * Names the stage still outstanding, not just "still working" — a slow explanation call is otherwise
 * indistinguishable from a hung page (F03 E3). Order matches the contract: intent, then offers
 * (supplier results are a sub-step of this one, not their own wait state), then explanation.
 */
function pendingLabel(stages: AssistantStages, strings: Strings): string {
  if (!stages.parsedIntent) return strings.searching
  if (!stages.rankedOffers) return strings.waitingForOffers
  return strings.waitingForExplanation
}

/**
 * One assistant turn, rendering only the stages that have actually arrived.
 *
 * A half-filled turn is the *normal* state here, not an edge case — the four stages land seconds
 * apart — so an absent stage renders nothing at all rather than a placeholder or a reserved gap.
 */
export interface AssistantTurnViewProps {
  turn: AssistantTurn
  /** Omitted when there's nowhere for a booking to go (e.g. component-level tests). */
  onBookOffer?: (offer: RankedOffer) => void
}

export function AssistantTurnView({ turn, onBookOffer }: AssistantTurnViewProps) {
  const { strings } = useLanguage()
  const { stages, status } = turn

  return (
    <article className="turn turn--assistant" aria-label={strings.resultsLabel}>
      {/* Stages arrive asynchronously, so they're announced rather than silently appearing. */}
      <div aria-live="polite" aria-atomic="false">
        {stages.parsedIntent && (
          <section className="stage stage--intent">
            <h3 className="stage__title">{strings.stageUnderstood}</h3>
            <p>
              {stages.parsedIntent.origin} → {stages.parsedIntent.destination} ·{' '}
              {stages.parsedIntent.departureDate} ·{' '}
              {stages.parsedIntent.passengerCount === 1
                ? `1 ${strings.traveller}`
                : `${stages.parsedIntent.passengerCount} ${strings.travellers}`}
            </p>
          </section>
        )}

        {stages.supplierResults.length > 0 && (
          <section className="stage stage--suppliers">
            <h3 className="stage__title">{strings.stageSuppliers}</h3>
            <ul className="supplier-list">
              {stages.supplierResults.map((result) => (
                <li key={result.supplierName} className="supplier-list__item">
                  <span className="supplier-list__name">{result.supplierName}</span>
                  <span className="supplier-list__status">{result.status}</span>
                  <span className="supplier-list__count">
                    {result.offerCount === 1 ? '1 offer' : `${result.offerCount} offers`}
                  </span>
                </li>
              ))}
            </ul>
          </section>
        )}

        {stages.rankedOffers && (
          <section className="stage stage--offers">
            <h3 className="stage__title">{strings.stageOffers}</h3>
            <ol className="offer-list">
              {stages.rankedOffers.map((offer) => (
                <OfferCard key={offer.offerId} offer={offer} onBook={onBookOffer} />
              ))}
            </ol>
            <OfferComparison offers={stages.rankedOffers} />
          </section>
        )}

        {stages.explanation && (
          <section className="stage stage--explanation">
            <h3 className="stage__title">{strings.stageWhy}</h3>
            {/* F06 owns the isClean:false presentation; text is already blanked server-side. */}
            <p className="explanation__text">{stages.explanation.text}</p>
          </section>
        )}
      </div>

      {status === 'streaming' && (
        <p className="turn__pending" role="status">
          {pendingLabel(stages, strings)}
        </p>
      )}

      {status === 'failed' && turn.failure && (
        <p className="turn__failure" role="alert">
          {turn.failure.message}
        </p>
      )}
    </article>
  )
}
