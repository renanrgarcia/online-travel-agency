import { useEffect, useState } from "react";
import { getBookingStatus, startBooking } from "../api";
import type { BookingCustomStatus, BookingOutput, BookingStatus, RankedOffer } from "../types";

interface Props {
  offer: RankedOffer;
  onClose: () => void;
}

const HAPPY_STEPS = [
  { key: "authorizing-payment", label: "Authorize payment" },
  { key: "creating-order", label: "Create order" },
  { key: "issuing-ticket", label: "Issue ticket" },
  { key: "sending-confirmation", label: "Send confirmation" },
  { key: "completed", label: "Completed" },
];

function parseJson<T>(text: string | null): T | null {
  if (!text) return null;
  try {
    return JSON.parse(text) as T;
  } catch {
    return null;
  }
}

export function BookingPanel({ offer, onClose }: Props) {
  const [email, setEmail] = useState("traveller@example.com");
  const [simulateFailure, setSimulateFailure] = useState(false);
  const [bookingId, setBookingId] = useState<string | null>(null);
  const [status, setStatus] = useState<BookingStatus | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (!bookingId || status?.runtimeStatus === "Completed" || status?.runtimeStatus === "Failed") return;

    const interval = setInterval(async () => {
      try {
        const next = await getBookingStatus(bookingId);
        setStatus(next);
      } catch (err) {
        setError(err instanceof Error ? err.message : "Failed to poll booking status.");
      }
    }, 900);

    return () => clearInterval(interval);
  }, [bookingId, status?.runtimeStatus]);

  async function handleConfirm() {
    setSubmitting(true);
    setError(null);
    const id = `booking-${Date.now()}`;
    const offerId = simulateFailure ? `${offer.offerId}-FAIL-TICKET` : offer.offerId;

    try {
      await startBooking({
        bookingId: id,
        offerId,
        travellerEmail: email,
        amount: offer.price,
        currency: offer.currency,
        paymentMethodToken: "tok_demo",
      });
      setBookingId(id);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to start booking.");
    } finally {
      setSubmitting(false);
    }
  }

  const customStatus = parseJson<BookingCustomStatus>(status?.customStatus ?? null);
  const output = parseJson<BookingOutput>(status?.output ?? null);
  const isCompensating = customStatus?.step === "compensating" || customStatus?.step === "failed";
  const currentStepIndex = HAPPY_STEPS.findIndex((s) => s.key === customStatus?.step);

  return (
    <div className="modal-backdrop" onClick={onClose}>
      <div className="modal" onClick={(e) => e.stopPropagation()}>
        <div className="modal-header">
          <h3>
            Book {offer.carrier} · {offer.price.toFixed(2)} {offer.currency}
          </h3>
          <button type="button" className="modal-close" onClick={onClose}>
            ×
          </button>
        </div>

        {!bookingId && (
          <div className="modal-body">
            <label className="field">
              Traveller email
              <input value={email} onChange={(e) => setEmail(e.target.value)} />
            </label>
            <label className="switch">
              <input
                type="checkbox"
                checked={simulateFailure}
                onChange={(e) => setSimulateFailure(e.target.checked)}
              />
              Simulate a ticketing failure (walks the compensation path: cancel order, void payment)
            </label>
            {error && <p className="error-text">{error}</p>}
            <button type="button" onClick={handleConfirm} disabled={submitting}>
              {submitting ? "Starting…" : "Confirm booking"}
            </button>
          </div>
        )}

        {bookingId && (
          <div className="modal-body">
            <p className="mono booking-id">{bookingId}</p>

            {!isCompensating && (
              <ol className="saga-steps">
                {HAPPY_STEPS.map((step, i) => {
                  const state =
                    currentStepIndex === -1
                      ? "pending"
                      : i < currentStepIndex
                        ? "done"
                        : i === currentStepIndex
                          ? "active"
                          : "pending";
                  return (
                    <li key={step.key} className={`saga-step saga-step--${state}`}>
                      <span className="pipeline-dot" />
                      {step.label}
                    </li>
                  );
                })}
              </ol>
            )}

            {isCompensating && (
              <div className="compensation-banner">
                <strong>Rolling back — {customStatus?.stage} failed</strong>
                <p>
                  {customStatus?.step === "compensating"
                    ? "Compensating actions in flight…"
                    : `Compensated: ${customStatus?.compensated?.join(", ") ?? "—"}`}
                </p>
              </div>
            )}

            {status?.runtimeStatus === "Completed" && output && (
              <div className={`booking-result ${output.Success ? "booking-result--ok" : "booking-result--fail"}`}>
                {output.Success ? (
                  <>
                    <strong>Booked.</strong>
                    <dl>
                      <dt>Authorization</dt>
                      <dd className="mono">{output.AuthorizationId}</dd>
                      <dt>Order</dt>
                      <dd className="mono">{output.OrderId}</dd>
                      <dt>Ticket</dt>
                      <dd className="mono">{output.TicketNumber}</dd>
                    </dl>
                  </>
                ) : (
                  <>
                    <strong>Booking failed at "{output.FailedStage}" — fully rolled back.</strong>
                    <p>{output.FailureReason}</p>
                    <dl>
                      {output.AuthorizationId && (
                        <>
                          <dt>Payment authorized then voided</dt>
                          <dd className="mono">{output.AuthorizationId}</dd>
                        </>
                      )}
                      {output.OrderId && (
                        <>
                          <dt>Order created then cancelled</dt>
                          <dd className="mono">{output.OrderId}</dd>
                        </>
                      )}
                    </dl>
                  </>
                )}
              </div>
            )}

            {error && <p className="error-text">{error}</p>}
          </div>
        )}
      </div>
    </div>
  );
}
