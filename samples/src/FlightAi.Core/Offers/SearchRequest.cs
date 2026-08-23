namespace FlightAi.Core.Offers;

/// <summary>
/// The typed, schema-validated output of the intent-parsing agent.
/// Natural language goes in, this comes out — nothing downstream of this point ever reads free text again.
/// </summary>
public sealed record SearchRequest
{
    public required string Origin { get; init; }
    public required string Destination { get; init; }
    public required DateOnly DepartureDate { get; init; }
    public DateOnly? ReturnDate { get; init; }
    public required TravellerCounts Travellers { get; init; }
    public CabinClass Cabin { get; init; } = CabinClass.Economy;
    public SearchPreferences Preferences { get; init; } = new();
}

public enum CabinClass
{
    Economy,
    PremiumEconomy,
    Business,
    First
}

public sealed record TravellerCounts(int Adults, int Children = 0, int Infants = 0);

public sealed record SearchPreferences(
    bool AvoidRedEyes = false,
    string? SeatPreference = null,
    int? MaxStops = null);
