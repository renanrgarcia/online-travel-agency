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
/// </para>
/// </summary>
public static class BookingActivities
{
    private static readonly ConcurrentDictionary<string, int> FlakyAttempts = new();

    [Function(nameof(AuthorizePayment))]
    public static AuthorizePaymentResult AuthorizePayment([ActivityTrigger] AuthorizePaymentInput input) =>
        new($"AUTH-{input.BookingId}");

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
    }
}
