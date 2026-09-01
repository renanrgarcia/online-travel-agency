using FlightAi.Core.Services.Pricing;

namespace FlightAi.Booking.Functions.Models;

/// <summary>
/// The <c>POST /api/bookings</c> HTTP body -- distinct from <see cref="BookingRequest"/> (the
/// orchestration's actual input) because this one carries the client-supplied <see cref="Amount"/> /
/// <see cref="Currency"/> that <see cref="PriceAssertion"/> exists to override (task 21). By the time a
/// <see cref="BookingRequest"/> is constructed, it holds the assertion's verified values, never these.
/// </summary>
public sealed record CreateBookingRequest(
    string BookingId,
    string OfferId,
    string TravellerEmail,
    decimal Amount,
    string Currency,
    string PaymentMethodToken,
    PriceAssertion? PriceAssertion);
