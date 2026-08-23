namespace FlightAi.Booking.Functions;

/// <summary>
/// The saga's input. <see cref="BookingId"/> doubles as the orchestration instance ID — see
/// <see cref="BookingTriggers.StartBooking"/> for why that is the idempotency mechanism.
/// </summary>
public sealed record BookingRequest(
    string BookingId,
    string OfferId,
    string TravellerEmail,
    decimal Amount,
    string Currency,
    string PaymentMethodToken);

public sealed record PaymentAuthorization(string AuthorizationId, bool Success);

public sealed record OrderConfirmation(string OrderId, bool Success);

public sealed record TicketRequest(string OrderId, string OfferId);

public sealed record TicketConfirmation(string TicketNumber, bool Success);

public sealed record ConfirmationRequest(string Email, string TicketNumber);

/// <summary>
/// The saga's output. On failure, whichever of <see cref="AuthorizationId"/> / <see cref="OrderId"/>
/// are non-null tell you exactly how far the booking got before it was rolled back — which is also
/// exactly what was compensated.
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
        new(true, authorizationId, orderId, ticketNumber, null, null);

    public static BookingResult Failed(string stage, string reason, string? authorizationId = null, string? orderId = null) =>
        new(false, authorizationId, orderId, null, stage, reason);
}
