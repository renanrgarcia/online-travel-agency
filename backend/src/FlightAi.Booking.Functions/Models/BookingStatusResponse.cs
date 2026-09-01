namespace FlightAi.Booking.Functions.Models;

/// <summary>
/// The <c>GET /api/bookings/{bookingId}</c> envelope. <see cref="CustomStatus"/> and <see cref="Output"/>
/// are passed through as the raw JSON strings Durable Task already produced (<c>OrchestrationMetadata</c>'s
/// <c>SerializedCustomStatus</c> / <c>SerializedOutput</c>) rather than re-parsed and re-embedded — the
/// client is expected to parse them, per <c>docs/reference/07-booking-saga.md</c>.
/// </summary>
public sealed record BookingStatusResponse(
    string BookingId,
    string RuntimeStatus,
    string? CustomStatus,
    string? Output,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastUpdatedAt);
