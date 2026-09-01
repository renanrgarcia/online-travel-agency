using FlightAi.Booking.Functions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;

namespace FlightAi.Booking.Functions;

/// <summary>
/// The saga's full sequence, <c>AuthorizePayment</c> → <c>CreateOrder</c> → <c>IssueTicket</c> →
/// <c>SendConfirmation</c>, each checkpointed, with compensation (task 16) on a later step's failure --
/// <c>VoidPayment</c> and/or <c>CancelOrder</c>, run in reverse order of completion
/// (<c>docs/reference/07-booking-saga.md</c>): whatever was built up last is undone first.
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
            // Nothing to compensate -- it's the first step (docs/reference/07-booking-saga.md, task 16 E3).
            context.SetCustomStatus(new { step = "failed", stage = nameof(BookingActivities.AuthorizePayment) });
            return BookingResult.Failed(nameof(BookingActivities.AuthorizePayment), ex.FailureDetails.ErrorMessage);
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
            return await Compensate(
                context, request, nameof(BookingActivities.CreateOrder), ex, authorized.AuthorizationId, orderId: null);
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
            return await Compensate(
                context, request, nameof(BookingActivities.IssueTicket), ex, authorized.AuthorizationId, order.OrderId);
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
            // the booking (docs/reference/07-booking-saga.md, task 16 E4).
            context.SetCustomStatus(new { step = "completed", warning = "confirmation email failed" });
            return BookingResult.Succeeded(authorized.AuthorizationId, order.OrderId, ticket.TicketNumber);
        }

        context.SetCustomStatus(new { step = "completed" });
        return BookingResult.Succeeded(authorized.AuthorizationId, order.OrderId, ticket.TicketNumber);
    }

    /// <summary>
    /// Undoes whatever completed before <paramref name="failedStage"/> failed, most-recent first:
    /// <c>CancelOrder</c> (if an order exists) then <c>VoidPayment</c> (if a payment was authorized).
    /// A compensating activity that itself exhausts its retries is surfaced in <c>customStatus</c> as an
    /// explicit warning rather than swallowed (task 16 E9) -- a failed rollback must be loud.
    /// </summary>
    private static async Task<BookingResult> Compensate(
        TaskOrchestrationContext context, BookingRequest request, string failedStage, TaskFailedException failure,
        string? authorizationId, string? orderId)
    {
        context.SetCustomStatus(new { step = "compensating", stage = failedStage });

        try
        {
            if (orderId is not null)
            {
                await context.CallActivityAsync<CancelOrderResult>(
                    nameof(BookingActivities.CancelOrder),
                    new CancelOrderInput(request.BookingId, request.OfferId, orderId),
                    RetryOptions);
            }

            if (authorizationId is not null)
            {
                await context.CallActivityAsync<VoidPaymentResult>(
                    nameof(BookingActivities.VoidPayment),
                    new VoidPaymentInput(request.BookingId, request.OfferId, authorizationId),
                    RetryOptions);
            }
        }
        catch (TaskFailedException compensationFailure)
        {
            context.SetCustomStatus(new
            {
                step = "failed",
                stage = failedStage,
                compensated = false,
                warning = $"compensation failed: {compensationFailure.TaskName} - {compensationFailure.FailureDetails.ErrorMessage}",
            });
            return BookingResult.Failed(failedStage, failure.FailureDetails.ErrorMessage, authorizationId, orderId);
        }

        context.SetCustomStatus(new { step = "failed", stage = failedStage, compensated = true });
        return BookingResult.Failed(failedStage, failure.FailureDetails.ErrorMessage, authorizationId, orderId);
    }
}
