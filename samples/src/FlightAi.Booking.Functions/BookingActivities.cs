using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace FlightAi.Booking.Functions;

/// <summary>
/// Simulated supplier and payment calls — a real integration replaces the body of each method, not
/// the shape of the saga around them. Deterministic failure triggers exist so the compensation path
/// is reproducible on demand rather than left to chance: an <see cref="BookingRequest.OfferId"/>
/// containing <c>FAIL-ORDER</c> fails order creation; one containing <c>FAIL-TICKET</c> fails
/// ticketing. Same convention <c>FlightAi.Core</c>'s mock connectors use for the same reason.
/// </summary>
public sealed class BookingActivities
{
    [Function(nameof(AuthorizePayment))]
    public async Task<PaymentAuthorization> AuthorizePayment([ActivityTrigger] BookingRequest request, FunctionContext context)
    {
        var logger = context.GetLogger(nameof(AuthorizePayment));
        await Task.Delay(300);

        var authorizationId = $"AUTH-{request.BookingId}";
        logger.LogInformation("Authorized {Amount} {Currency} for booking {BookingId} -> {AuthorizationId}",
            request.Amount, request.Currency, request.BookingId, authorizationId);
        return new PaymentAuthorization(authorizationId, Success: true);
    }

    [Function(nameof(CreateOrder))]
    public async Task<OrderConfirmation> CreateOrder([ActivityTrigger] BookingRequest request, FunctionContext context)
    {
        var logger = context.GetLogger(nameof(CreateOrder));
        await Task.Delay(300);

        if (request.OfferId.Contains("FAIL-ORDER", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("Order creation deliberately failing for offer {OfferId}", request.OfferId);
            throw new InvalidOperationException($"Supplier rejected order creation for offer '{request.OfferId}' (offer likely expired).");
        }

        var orderId = $"ORD-{request.BookingId}";
        logger.LogInformation("Order created for booking {BookingId} -> {OrderId}", request.BookingId, orderId);
        return new OrderConfirmation(orderId, Success: true);
    }

    [Function(nameof(IssueTicket))]
    public async Task<TicketConfirmation> IssueTicket([ActivityTrigger] TicketRequest request, FunctionContext context)
    {
        var logger = context.GetLogger(nameof(IssueTicket));
        await Task.Delay(300);

        if (request.OfferId.Contains("FAIL-TICKET", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("Ticketing deliberately failing for order {OrderId}", request.OrderId);
            throw new InvalidOperationException($"Ticketing system rejected order '{request.OrderId}'.");
        }

        var ticketNumber = $"TKT-{request.OrderId}";
        logger.LogInformation("Ticket issued for order {OrderId} -> {TicketNumber}", request.OrderId, ticketNumber);
        return new TicketConfirmation(ticketNumber, Success: true);
    }

    [Function(nameof(CancelOrder))]
    public async Task CancelOrder([ActivityTrigger] string orderId, FunctionContext context)
    {
        var logger = context.GetLogger(nameof(CancelOrder));
        await Task.Delay(150);
        logger.LogWarning("COMPENSATING: order {OrderId} cancelled", orderId);
    }

    [Function(nameof(VoidPayment))]
    public async Task VoidPayment([ActivityTrigger] string authorizationId, FunctionContext context)
    {
        var logger = context.GetLogger(nameof(VoidPayment));
        await Task.Delay(150);
        logger.LogWarning("COMPENSATING: payment authorization {AuthorizationId} voided", authorizationId);
    }

    [Function(nameof(SendConfirmation))]
    public async Task SendConfirmation([ActivityTrigger] ConfirmationRequest request, FunctionContext context)
    {
        var logger = context.GetLogger(nameof(SendConfirmation));
        await Task.Delay(100);
        logger.LogInformation("Confirmation for ticket {TicketNumber} sent to {Email}", request.TicketNumber, request.Email);
    }
}
