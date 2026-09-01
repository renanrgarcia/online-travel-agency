using FlightAi.Booking.Functions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;

namespace FlightAi.Booking.Functions;

/// <summary>
/// Task 15: the saga's happy-path sequence, <c>AuthorizePayment</c> → <c>CreateOrder</c> →
/// <c>IssueTicket</c> → <c>SendConfirmation</c>, each checkpointed. Compensation on a later failure
/// (<c>VoidPayment</c>, <c>CancelOrder</c>) is task 16 -- this orchestrator only carries a booking as
/// far as it will go and reports where it stopped.
/// <para>
/// Durable replays this method's code on every checkpoint resume, so it contains no
/// <see cref="DateTime.Now"/>, <see cref="Guid.NewGuid"/>, or direct I/O (task 15 E7) -- every ID and
/// every side effect lives in the activities it calls.
/// </para>
/// </summary>
public static class BookingOrchestrator
{
    private static readonly TaskOptions RetryOptions = TaskOptions.FromRetryPolicy(
        new RetryPolicy(maxNumberOfAttempts: 3, firstRetryInterval: TimeSpan.FromSeconds(2), backoffCoefficient: 2.0));

    [Function(nameof(RunBookingSaga))]
    public static async Task<BookingResult> RunBookingSaga([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var request = context.GetInput<BookingRequest>()!;

        context.SetCustomStatus(new { step = "authorizing-payment" });
        AuthorizePaymentResult authorized;
        try
        {
            authorized = await context.CallActivityAsync<AuthorizePaymentResult>(
                nameof(BookingActivities.AuthorizePayment),
                new AuthorizePaymentInput(request.BookingId, request.Amount, request.Currency, request.PaymentMethodToken),
                RetryOptions);
        }
        catch (TaskFailedException ex)
        {
            return Failed(context, nameof(BookingActivities.AuthorizePayment), ex);
        }

        context.SetCustomStatus(new { step = "creating-order" });
        CreateOrderResult order;
        try
        {
            order = await context.CallActivityAsync<CreateOrderResult>(
                nameof(BookingActivities.CreateOrder),
                new CreateOrderInput(request.BookingId, request.OfferId),
                RetryOptions);
        }
        catch (TaskFailedException ex)
        {
            return Failed(context, nameof(BookingActivities.CreateOrder), ex, authorized.AuthorizationId);
        }

        context.SetCustomStatus(new { step = "issuing-ticket" });
        IssueTicketResult ticket;
        try
        {
            ticket = await context.CallActivityAsync<IssueTicketResult>(
                nameof(BookingActivities.IssueTicket),
                new IssueTicketInput(request.BookingId, request.OfferId, order.OrderId),
                RetryOptions);
        }
        catch (TaskFailedException ex)
        {
            return Failed(context, nameof(BookingActivities.IssueTicket), ex, authorized.AuthorizationId, order.OrderId);
        }

        context.SetCustomStatus(new { step = "sending-confirmation" });
        try
        {
            await context.CallActivityAsync(
                nameof(BookingActivities.SendConfirmation),
                new SendConfirmationInput(request.BookingId, request.TravellerEmail, ticket.TicketNumber),
                RetryOptions);
        }
        catch (TaskFailedException)
        {
            // Not compensated -- the ticket is already real; a failed confirmation email doesn't unwind
            // the booking (docs/reference/07-booking-saga.md).
            context.SetCustomStatus(new { step = "completed", warning = "confirmation email failed" });
            return BookingResult.Succeeded(authorized.AuthorizationId, order.OrderId, ticket.TicketNumber);
        }

        context.SetCustomStatus(new { step = "completed" });
        return BookingResult.Succeeded(authorized.AuthorizationId, order.OrderId, ticket.TicketNumber);
    }

    private static BookingResult Failed(
        TaskOrchestrationContext context, string stage, TaskFailedException ex,
        string? authorizationId = null, string? orderId = null)
    {
        context.SetCustomStatus(new { step = "failed", stage });
        return BookingResult.Failed(stage, ex.FailureDetails.ErrorMessage, authorizationId, orderId);
    }
}
