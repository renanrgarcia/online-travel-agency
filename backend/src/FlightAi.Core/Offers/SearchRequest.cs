namespace FlightAi.Core.Offers;

/// <summary>
/// The typed output of intent parsing (task 10), and the input every <c>ISupplierConnector</c>
/// searches against. <see cref="Language"/> is inferred from the traveller's own query, never asked
/// for separately, and carries through to task 11's explanation agent so the reply comes back in the
/// same language the question was asked in.
/// </summary>
public sealed record SearchRequest(
    string Origin,
    string Destination,
    DateOnly DepartureDate,
    int PassengerCount,
    string Language);
