namespace FlightAi.Booking.Functions.Models;

public sealed record AuthorizePaymentInput(string BookingId, decimal Amount, string Currency, string PaymentMethodToken);
public sealed record AuthorizePaymentResult(string AuthorizationId);

public sealed record CreateOrderInput(string BookingId, string OfferId);
public sealed record CreateOrderResult(string OrderId);

public sealed record IssueTicketInput(string BookingId, string OfferId, string OrderId);
public sealed record IssueTicketResult(string TicketNumber);

public sealed record SendConfirmationInput(string BookingId, string TravellerEmail, string TicketNumber);

public sealed record VoidPaymentInput(string BookingId, string OfferId, string AuthorizationId);
public sealed record VoidPaymentResult(bool AlreadyVoided);

public sealed record CancelOrderInput(string BookingId, string OfferId, string OrderId);
public sealed record CancelOrderResult(bool AlreadyCancelled);
