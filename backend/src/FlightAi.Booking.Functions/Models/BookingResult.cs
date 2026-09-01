namespace FlightAi.Booking.Functions.Models;

/// <summary>
/// The orchestration's typed output. PascalCase is intentional — Durable Task's default serializer
/// preserves C# member casing rather than converting it, and <c>docs/reference/07-booking-saga.md</c>
/// documents this exact shape as the client contract. On failure, whichever of
/// <see cref="AuthorizationId"/> / <see cref="OrderId"/> are non-null says how far the booking got
/// before it stopped.
/// </summary>
public sealed record BookingResult(
    bool Success,
    string? AuthorizationId,
    string? OrderId,
    string? TicketNumber,
    string? FailedStage,
    string? FailureReason)
{
    public static BookingResult Succeeded(string authorizationId, string orderId, string ticketNumber) =>
        new(true, authorizationId, orderId, ticketNumber, FailedStage: null, FailureReason: null);

    public static BookingResult Failed(
        string failedStage, string? failureReason, string? authorizationId = null, string? orderId = null) =>
        new(false, authorizationId, orderId, TicketNumber: null, failedStage, failureReason);
}
