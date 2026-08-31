namespace FlightAi.Booking.Functions.Models;

public sealed record BookingRequest(
    string BookingId,
    string OfferId,
    string TravellerEmail,
    decimal Amount,
    string Currency,
    string PaymentMethodToken);
