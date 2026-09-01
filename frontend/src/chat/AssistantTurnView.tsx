import { useLanguage } from '../i18n/LanguageProvider'
import type { AssistantTurn } from './types'
import { hasAnyStage } from './types'

/**
 * One assistant turn, rendering only the stages that have actually arrived.
 *
 * A half-filled turn is the *normal* state here, not an edge case — the four stages land seconds
 * apart — so an absent stage renders nothing at all rather than a placeholder or a reserved gap.
 */
export function AssistantTurnView({ turn }: { turn: AssistantTurn }) {
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
            {/* Task F04 replaces this with real cards and the comparison view. */}
            <ol className="offer-list">
              {stages.rankedOffers.map((offer) => (
                <li key={offer.offerId} className="offer-list__item">
                  {offer.offerId} · {offer.price} {offer.currency}
                </li>
              ))}
            </ol>
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
          {hasAnyStage(stages) ? strings.stillSearching : strings.searching}
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
