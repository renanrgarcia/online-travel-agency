import { useLanguage } from '../i18n/LanguageProvider'
import type { AssistantStages, AssistantTurn } from './types'
import type { Strings } from '../i18n/strings'
import type { RankedOffer, SupplierStatus } from '../api/contract'
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

function supplierStatusLabel(status: SupplierStatus, strings: Strings): string {
  switch (status) {
    case 'Succeeded':
      return strings.supplierStatusSucceeded
    case 'PartialSuccess':
      return strings.supplierStatusPartialSuccess
    case 'Failed':
      return strings.supplierStatusFailed
    case 'TimedOut':
      return strings.supplierStatusTimedOut
    case 'Cancelled':
      return strings.supplierStatusCancelled
    case 'SkippedCircuitOpen':
      return strings.supplierStatusSkippedCircuitOpen
    case 'SkippedBudgetExhausted':
      return strings.supplierStatusSkippedBudgetExhausted
  }
}

/**
 * A dead supplier is a designed, reported outcome, not an error (F06 E1) — so it gets a distinct but
 * calm treatment, not the alarm-red reserved for turn-level failures. "Skipped" is its own, quieter
 * category: the orchestrator made a deliberate choice not to call that supplier at all, which reads
 * differently from a call that was made and didn't succeed (F06 E2).
 */
function supplierStatusCategory(status: SupplierStatus): 'ok' | 'warn' | 'skip' {
  switch (status) {
    case 'Succeeded':
    case 'PartialSuccess':
      return 'ok'
    case 'Failed':
    case 'TimedOut':
    case 'Cancelled':
      return 'warn'
    case 'SkippedCircuitOpen':
    case 'SkippedBudgetExhausted':
      return 'skip'
  }
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
                <li
                  key={result.supplierName}
                  className={`supplier-list__item supplier-list__item--${supplierStatusCategory(result.status)}`}
                >
                  <span className="supplier-list__name">{result.supplierName}</span>
                  <span className="supplier-list__status">{supplierStatusLabel(result.status, strings)}</span>
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
            {stages.rankedOffers.length === 0 ? (
              // Every supplier failed or none had a match — a designed, calm outcome (F06 E5), not
              // the same "still working" state as a stage that hasn't arrived yet.
              <p className="offer-list__empty">{strings.noOffersFound}</p>
            ) : (
              <>
                <ol className="offer-list">
                  {stages.rankedOffers.map((offer) => (
                    <OfferCard key={offer.offerId} offer={offer} onBook={onBookOffer} />
                  ))}
                </ol>
                <OfferComparison offers={stages.rankedOffers} />
              </>
            )}
          </section>
        )}

        {stages.explanation && (
          <section className="stage stage--explanation">
            <h3 className="stage__title">{strings.stageWhy}</h3>
            {stages.explanation.isClean ? (
              <p className="explanation__text">{stages.explanation.text}</p>
            ) : (
              // isClean:false means `text` is blanked server-side (task 02/18) -- rendering `raw`
              // here instead, even as a fallback, would undo that guard in the one place it matters
              // most (F06's locked decision, E3). Say plainly that nothing is available; no jargon
              // about tokens or guards leaking into user-facing copy (E4).
              <p className="explanation__unavailable">{strings.explanationUnavailable}</p>
            )}
            {/* Closed by default -- opt-in debug view of the model's pre-resolution output, clearly
                labelled as raw rather than as an answer (F06 E8). */}
            <details className="explanation__debug">
              <summary>{strings.explanationShowRaw}</summary>
              <p className="explanation__raw-label">{strings.explanationRawLabel}</p>
              <pre className="explanation__raw">{stages.explanation.raw}</pre>
            </details>
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
