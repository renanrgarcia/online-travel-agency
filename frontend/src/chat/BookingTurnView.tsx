import { useState, type FormEvent } from 'react'

import { useLanguage } from '../i18n/LanguageProvider'
import type { Strings } from '../i18n/strings'
import type { BookingCustomStatus } from '../api/bookingContract'
import type { RankedOffer } from '../api/contract'
import { formatDuration, formatPrice, formatStops } from './offerFormatting'
import type { BookingTurn } from './types'

function stepLabel(step: BookingCustomStatus['step'] | undefined, strings: Strings): string | null {
  switch (step) {
    case 'authorizing-payment':
      return strings.bookingStepAuthorizingPayment
    case 'creating-order':
      return strings.bookingStepCreatingOrder
    case 'issuing-ticket':
      return strings.bookingStepIssuingTicket
    case 'sending-confirmation':
      return strings.bookingStepSendingConfirmation
    case 'compensating':
      return strings.bookingStepCompensating
    default:
      return null
  }
}

export interface BookingTurnViewProps {
  turn: BookingTurn
  onConfirm: (turnId: string, bookingId: string, offer: RankedOffer, travellerEmail: string) => void
  onCancel: (turnId: string) => void
}

export function BookingTurnView({ turn, onConfirm, onCancel }: BookingTurnViewProps) {
  const { strings } = useLanguage()
  const [email, setEmail] = useState('')
  const [emailError, setEmailError] = useState<string | null>(null)

  const handleSubmit = (event: FormEvent) => {
    event.preventDefault()
    if (email.trim().length === 0) {
      setEmailError(strings.bookingEmailRequired)
      return
    }
    onConfirm(turn.id, turn.bookingId, turn.offer, email.trim())
  }

  return (
    <article className="turn turn--booking" aria-label={strings.bookOffer}>
      <p className="booking__offer">
        {turn.offer.offerId} · {formatPrice(turn.offer.price, turn.offer.currency)} ·{' '}
        {formatDuration(turn.offer.durationMinutes)} · {formatStops(turn.offer.stops, strings)}
      </p>

      {turn.status === 'collecting-details' && (
        <form className="booking-form" onSubmit={handleSubmit}>
          <label className="booking-form__label" htmlFor={`${turn.id}-email`}>
            {strings.bookingTravellerEmailLabel}
          </label>
          <div className="booking-form__row">
            <input
              id={`${turn.id}-email`}
              className="booking-form__input"
              type="email"
              autoComplete="email"
              value={email}
              aria-invalid={emailError !== null}
              aria-describedby={emailError ? `${turn.id}-email-error` : undefined}
              placeholder={strings.bookingTravellerEmailPlaceholder}
              onChange={(event) => {
                setEmail(event.target.value)
                if (emailError) setEmailError(null)
              }}
            />
            <button className="booking-form__submit" type="submit">
              {strings.bookingConfirm}
            </button>
            <button
              className="booking-form__cancel"
              type="button"
              onClick={() => onCancel(turn.id)}
            >
              {strings.bookingCancel}
            </button>
          </div>
          {emailError && (
            <p className="composer__error" id={`${turn.id}-email-error`} role="alert">
              {emailError}
            </p>
          )}
        </form>
      )}

      {turn.status === 'submitting' && (
        <p className="turn__pending" role="status">
          {strings.bookingSubmitting}
        </p>
      )}

      {turn.status === 'polling' && (
        <p className="turn__pending" role="status">
          {stepLabel(turn.customStatus?.step, strings) ?? strings.bookingSubmitting}
        </p>
      )}

      {turn.status === 'booked' && turn.output && (
        <div className="booking-outcome booking-outcome--success">
          <p className="booking-outcome__title">{strings.bookingBookedTitle}</p>
          <p>
            {strings.bookingTicketNumber}: <strong>{turn.output.TicketNumber}</strong>
          </p>
        </div>
      )}

      {turn.status === 'saga-failed' && turn.output && (
        <div className="booking-outcome booking-outcome--failed">
          <p className="booking-outcome__title">{strings.bookingFailedTitle}</p>
          <p>{turn.output.FailureReason}</p>
          {/* Compensation is stated in plain language, not a status code (F05's locked decision) --
              a payment-authorized-but-not-refunded booking is the worst message this product could
              send by omission. */}
          <p className="booking-outcome__compensation">
            {turn.customStatus?.warning
              ? strings.bookingCompensationFailed
              : turn.customStatus?.compensated
                ? strings.bookingCompensated
                : strings.bookingNotCompensated}
          </p>
        </div>
      )}

      {turn.status === 'error' && (
        <div className="booking-outcome booking-outcome--failed">
          <p className="booking-outcome__title">{strings.bookingErrorTitle}</p>
          <p role="alert">
            {turn.error?.message === 'not-found'
              ? strings.bookingErrorNotFound
              : turn.error?.message === 'network'
                ? strings.bookingErrorNetwork
                : (turn.error?.message ?? strings.bookingErrorNetwork)}
          </p>
        </div>
      )}
    </article>
  )
}
