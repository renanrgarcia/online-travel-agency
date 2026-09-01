using System.Collections.Concurrent;
using FlightAi.Booking.Functions.Models;
using Microsoft.Azure.Functions.Worker;

namespace FlightAi.Booking.Functions;

/// <summary>
/// Mocked booking-step activities -- no real payment gateway, order system, or ticketing system.
/// IDs are generated here, deterministically from <c>BookingId</c>, never in the orchestrator
/// (<c>docs/reference/07-booking-saga.md</c>: non-determinism stays in activities).
/// <para>
/// Deterministic failure injection mirrors the mock supplier connectors' convention
/// (<c>docs/reference/03-suppliers-and-budget.md</c>): an <c>offerId</c> containing <c>FAIL-ORDER</c> or
/// <c>FAIL-TICKET</c> always fails that step (task 15 E5); one containing <c>FLAKY-ORDER</c> fails the
/// first two attempts and succeeds on the third, to exercise the retry policy itself (task 15 E4).
/// <c>FAIL-VOID</c> / <c>FAIL-CANCEL</c> apply the same convention to the compensating activities
/// (task 16 E9) -- a compensating activity that itself keeps failing must surface loudly, not vanish.
/// A <c>paymentMethodToken</c> containing <c>FAIL-AUTH</c> fails payment authorization itself (task 16
/// E3) -- a bad token is the realistic trigger for that step, not the offer. Likewise a
/// <c>travellerEmail</c> containing <c>FAIL-CONFIRM</c> fails the confirmation email (task 16 E4).
/// </para>
/// <para>
/// <see cref="VoidPayment"/> and <see cref="CancelOrder"/> are idempotent by design (task 16 E10):
/// Durable retries a compensating activity under the same retry policy as everything else, and a
/// second <c>VoidPayment</c> for an authorization already voided must be a no-op, never a second
/// refund.
/// </para>
/// </summary>
public static class BookingActivities
{
    private static readonly ConcurrentDictionary<string, int> FlakyAttempts = new();
    private static readonly ConcurrentDictionary<string, bool> VoidedAuthorizations = new();
    private static readonly ConcurrentDictionary<string, bool> CancelledOrders = new();

    [Function(nameof(AuthorizePayment))]
    public static AuthorizePaymentResult AuthorizePayment([ActivityTrigger] AuthorizePaymentInput input)
    {
        if (input.PaymentMethodToken.Contains("FAIL-AUTH", StringComparison.Ordinal))
            throw new InvalidOperationException($"Payment authorization failed for token '{input.PaymentMethodToken}'.");

        return new AuthorizePaymentResult($"AUTH-{input.BookingId}");
    }

    [Function(nameof(CreateOrder))]
    public static CreateOrderResult CreateOrder([ActivityTrigger] CreateOrderInput input)
    {
        if (input.OfferId.Contains("FAIL-ORDER", StringComparison.Ordinal))
            throw new InvalidOperationException($"Order creation failed for offer '{input.OfferId}'.");

        if (input.OfferId.Contains("FLAKY-ORDER", StringComparison.Ordinal))
        {
            var attempt = FlakyAttempts.AddOrUpdate(input.BookingId, 1, static (_, count) => count + 1);
            if (attempt < 3)
                throw new InvalidOperationException(
                    $"Transient order creation failure (attempt {attempt}) for offer '{input.OfferId}'.");
        }

        return new CreateOrderResult($"ORD-{input.BookingId}");
    }

    [Function(nameof(IssueTicket))]
    public static IssueTicketResult IssueTicket([ActivityTrigger] IssueTicketInput input)
    {
        if (input.OfferId.Contains("FAIL-TICKET", StringComparison.Ordinal))
            throw new InvalidOperationException($"Ticket issuance failed for offer '{input.OfferId}'.");

        return new IssueTicketResult($"TKT-{input.OrderId}");
    }

    // Deliberately not compensated on failure (docs/reference/07-booking-saga.md) -- the ticket is
    // already real.
    [Function(nameof(SendConfirmation))]
    public static void SendConfirmation([ActivityTrigger] SendConfirmationInput input)
    {
        if (input.TravellerEmail.Contains("FAIL-CONFIRM", StringComparison.Ordinal))
            throw new InvalidOperationException($"Sending confirmation to '{input.TravellerEmail}' failed.");
    }

    [Function(nameof(VoidPayment))]
    public static VoidPaymentResult VoidPayment([ActivityTrigger] VoidPaymentInput input)
    {
        if (input.OfferId.Contains("FAIL-VOID", StringComparison.Ordinal))
            throw new InvalidOperationException($"Voiding payment '{input.AuthorizationId}' failed.");

        var alreadyVoided = !VoidedAuthorizations.TryAdd(input.AuthorizationId, true);
        return new VoidPaymentResult(alreadyVoided);
    }

    [Function(nameof(CancelOrder))]
    public static CancelOrderResult CancelOrder([ActivityTrigger] CancelOrderInput input)
    {
        if (input.OfferId.Contains("FAIL-CANCEL", StringComparison.Ordinal))
            throw new InvalidOperationException($"Cancelling order '{input.OrderId}' failed.");

        var alreadyCancelled = !CancelledOrders.TryAdd(input.OrderId, true);
        return new CancelOrderResult(alreadyCancelled);
    }
}
