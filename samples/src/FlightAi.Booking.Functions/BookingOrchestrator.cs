using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace FlightAi.Booking.Functions;

/// <summary>
/// Selecting an offer, creating an order, taking payment, and issuing the ticket are four distinct
/// operations with four distinct failure modes, and they can fail independently after the customer has
/// already been charged. Every one of them needs an idempotency key, a durable state machine, and a
/// compensating action — this orchestration is that requirement as code. If the host crashes between
/// any two steps, Durable Functions checkpoints mean it resumes exactly where it left off on restart —
/// completed activities are never re-executed, only the orchestrator's control flow replays.
/// </summary>
public static class BookingOrchestrator
{
    private static readonly TaskOptions StandardRetry = TaskOptions.FromRetryPolicy(
        new RetryPolicy(maxNumberOfAttempts: 3, firstRetryInterval: TimeSpan.FromSeconds(2), backoffCoefficient: 2.0));

    [Function(nameof(RunBookingSaga))]
    public static async Task<BookingResult> RunBookingSaga([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var request = context.GetInput<BookingRequest>()
                      ?? throw new ArgumentException("Booking saga started with no input.");
        var logger = context.CreateReplaySafeLogger(nameof(BookingOrchestrator));

        // Step 1 — authorize payment. Nothing to compensate yet if this fails; it's the first step.
        context.SetCustomStatus(new { step = "authorizing-payment" });
        PaymentAuthorization payment;
        try
        {
            payment = await context.CallActivityAsync<PaymentAuthorization>(
                nameof(BookingActivities.AuthorizePayment), request, StandardRetry);
        }
        catch (TaskFailedException ex)
        {
            logger.LogError("Payment authorization failed for booking {BookingId}: {Reason}", request.BookingId, ex.Message);
            context.SetCustomStatus(new { step = "failed", stage = "payment-authorization" });
            return BookingResult.Failed("payment-authorization", ex.Message);
        }

        // Step 2 — create the order. If this fails, the payment authorization from step 1 must be
        // voided — it is real money held against a booking that will never complete.
        context.SetCustomStatus(new { step = "creating-order" });
        OrderConfirmation order;
        try
        {
            order = await context.CallActivityAsync<OrderConfirmation>(
                nameof(BookingActivities.CreateOrder), request, StandardRetry);
        }
        catch (TaskFailedException ex)
        {
            logger.LogWarning("Order creation failed for booking {BookingId}; voiding payment {AuthorizationId}",
                request.BookingId, payment.AuthorizationId);
            context.SetCustomStatus(new { step = "compensating", stage = "order-creation" });
            await context.CallActivityAsync(nameof(BookingActivities.VoidPayment), payment.AuthorizationId);

            context.SetCustomStatus(new { step = "failed", stage = "order-creation", compensated = new[] { "payment" } });
            return BookingResult.Failed("order-creation", ex.Message, authorizationId: payment.AuthorizationId);
        }

        // Step 3 — issue the ticket. If this fails, both prior steps must unwind: cancel the order,
        // then void the payment. This closes the "payment authorised, ticket not issued" failure mode
        // with a compensating action instead of a manual ops queue.
        context.SetCustomStatus(new { step = "issuing-ticket" });
        TicketConfirmation ticket;
        try
        {
            ticket = await context.CallActivityAsync<TicketConfirmation>(
                nameof(BookingActivities.IssueTicket), new TicketRequest(order.OrderId, request.OfferId), StandardRetry);
        }
        catch (TaskFailedException ex)
        {
            logger.LogWarning(
                "Ticketing failed for booking {BookingId}; cancelling order {OrderId} and voiding payment {AuthorizationId}",
                request.BookingId, order.OrderId, payment.AuthorizationId);
            context.SetCustomStatus(new { step = "compensating", stage = "ticketing" });
            await context.CallActivityAsync(nameof(BookingActivities.CancelOrder), order.OrderId);
            await context.CallActivityAsync(nameof(BookingActivities.VoidPayment), payment.AuthorizationId);

            context.SetCustomStatus(new { step = "failed", stage = "ticketing", compensated = new[] { "order", "payment" } });
            return BookingResult.Failed("ticketing", ex.Message, authorizationId: payment.AuthorizationId, orderId: order.OrderId);
        }

        // Step 4 — send the confirmation. A failure here does not roll back anything above: the
        // ticket is real and the traveller is booked, whether or not the email arrives.
        context.SetCustomStatus(new { step = "sending-confirmation" });
        try
        {
            await context.CallActivityAsync(nameof(BookingActivities.SendConfirmation),
                new ConfirmationRequest(request.TravellerEmail, ticket.TicketNumber));
        }
        catch (TaskFailedException ex)
        {
            logger.LogWarning("Confirmation notification failed for booking {BookingId}: {Reason} — booking still stands.",
                request.BookingId, ex.Message);
            context.SetCustomStatus(new { step = "completed", warning = "confirmation-notification-failed" });
            return BookingResult.Succeeded(payment.AuthorizationId, order.OrderId, ticket.TicketNumber);
        }

        context.SetCustomStatus(new { step = "completed" });
        return BookingResult.Succeeded(payment.AuthorizationId, order.OrderId, ticket.TicketNumber);
    }
}
