using FlightAi.Agents.Services;

namespace FlightAi.Api;

/// <summary>
/// The default <see cref="OfflineChatClient"/> registered for this API, so <c>GET /api/search/stream</c>
/// answers a query out of the box for manual <c>curl</c> testing (task 13 E1) without a real model or
/// API key -- task 17 replaces this registration with a real <c>IChatClient</c>, nothing else changes.
/// <para>
/// The explanation response below references the exact tokens the top three mock offers (task 05) will
/// actually resolve to under default scoring weights: this only works because the mock connectors are
/// deterministic, so the same query always ranks the same offers in the same order.
/// </para>
/// </summary>
public static class DemoOfflineChatClient
{
    public const string DemoQuery = "cheapest flight from São Paulo to Lisbon";

    public static OfflineChatClient Create() => new OfflineChatClient()
        .RegisterResponse(
            "São Paulo",
            """{"Origin":"GRU","Destination":"LIS","DepartureDate":"2027-03-12","PassengerCount":2,"Language":"en"}""")
        .RegisterResponse(
            "Offer LCC-002",
            "The best value is {{PRICE_LCC-002}}, taking {{DURATION_LCC-002}} with {{STOPS_LCC-002}} " +
            "({{REFUNDABLE_LCC-002}}). A cheaper option is {{PRICE_LCC-001}}, and a fully refundable " +
            "choice is {{PRICE_GDS-001}}.");
}
