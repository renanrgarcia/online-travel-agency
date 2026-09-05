using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml;
using FlightAi.Core.Interfaces.Suppliers;
using FlightAi.Core.Models.Offers;
using FlightAi.Core.Models.Suppliers;

namespace FlightAi.Core.Services.Suppliers;

/// <summary>
/// The one real supplier alongside the mock connectors (task 25) -- Duffel's test mode only, never
/// live, per docs/reference/12-supplier-api-options.md's locked recommendation. Registered as a typed
/// <see cref="HttpClient"/> in <c>Program.cs</c>, which is where the base address, bearer token, and
/// <c>Duffel-Version</c> header are configured; this class only knows how to build one request and map
/// one response, matching every other <see cref="ISupplierConnector"/>'s single responsibility.
/// </summary>
public sealed class DuffelConnector(HttpClient httpClient) : ISupplierConnector
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string Name => "Duffel";

    public async Task<SupplierSearchResult> SearchAsync(SearchRequest request, CancellationToken cancellationToken)
    {
        // Guaranteed non-null by the time a connector is ever called -- IntentAgent.Validate rejects a
        // null DepartureDate before SearchPipeline reaches the orchestrator at all (task 17). Checked
        // defensively anyway, since "return, don't throw" (task 04) shouldn't depend on a caller
        // upholding an invariant this connector can't see enforced.
        if (request.DepartureDate is not { } departureDate)
            return SupplierSearchResult.Failure("Duffel search requires a departure date");

        var body = new DuffelOfferRequestBody(new DuffelOfferRequestData(
            Slices: [new DuffelSlice(request.Origin, request.Destination, departureDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))],
            Passengers: [.. Enumerable.Repeat(new DuffelPassenger("adult"), Math.Max(1, request.PassengerCount))]));

        HttpResponseMessage response;
        try
        {
            response = await httpClient.PostAsJsonAsync("air/offer_requests?return_offers=true", body, JsonOptions, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // One linked token in play here (the orchestrator's per-connector timeout, task 06/07) --
            // this connector can't tell a real caller cancellation from a timeout, and doesn't need to;
            // only the orchestrator can attribute it, the same way the mock connectors already work.
            return SupplierSearchResult.Cancelled();
        }
        catch (HttpRequestException ex)
        {
            return SupplierSearchResult.Failure($"Duffel request failed: {ex.Message}");
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            return SupplierSearchResult.Failure($"Duffel returned {(int)response.StatusCode}: {errorBody}");
        }

        DuffelOfferRequestResponse? parsed;
        try
        {
            parsed = await response.Content.ReadFromJsonAsync<DuffelOfferRequestResponse>(JsonOptions, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return SupplierSearchResult.Cancelled();
        }
        catch (JsonException ex)
        {
            return SupplierSearchResult.Failure($"Duffel response was not valid JSON: {ex.Message}");
        }

        // No offers is a valid answer (task 04's own SupplierOutcome.Success doc comment), not a
        // failure -- Duffel returning an empty list for a route with no live-mode fares is exactly that.
        var duffelOffers = parsed?.Data?.Offers ?? [];
        var offers = duffelOffers.Select(TryMapOffer).OfType<Offer>().ToList();
        return SupplierSearchResult.Success(offers);
    }

    /// <summary>Maps one Duffel offer into the canonical <see cref="Offer"/>, or returns null for a
    /// specific offer this connector can't confidently map -- a malformed price or duration on one
    /// offer shouldn't fail every other offer in the same response.</summary>
    private static Offer? TryMapOffer(DuffelOffer offer)
    {
        if (offer.Slices.Count == 0 || offer.Slices[0].Duration is not { } durationText || offer.Slices[0].Segments.Count == 0)
            return null;

        try
        {
            var slice = offer.Slices[0];
            return new Offer(
                // "Duffel-" prefix follows the same per-connector uniqueness convention the mocks use
                // (task 05) -- Duffel's own IDs are already globally unique, but a shared prefix keeps
                // every offer's originating connector visible at a glance, same as "GDS-"/"NDC-"/"LCC-".
                OfferId: $"Duffel-{offer.Id}",
                Price: decimal.Parse(offer.TotalAmount, CultureInfo.InvariantCulture),
                Currency: offer.TotalCurrency,
                Duration: XmlConvert.ToTimeSpan(durationText),
                Stops: Math.Max(0, slice.Segments.Count - 1),
                Refundable: offer.Conditions?.RefundBeforeDeparture?.Allowed ?? false,
                // No margin concept on Duffel's side -- Margin defaults to zero for every connector
                // until deliberately turned on (docs/reference/04-ranking.md), real or mock alike.
                Margin: 0m,
                ExpiresAt: offer.ExpiresAt,
                // The real, specific airport this offer uses -- distinct from the traveller's searched
                // origin/destination, which may be a metro/city code (e.g. "SAO") covering several
                // airports. First segment's origin, last segment's destination, so a connection within
                // the slice doesn't lose the actual departure/arrival airport to a mid-journey one.
                OriginAirport: slice.Segments[0].Origin?.IataCode,
                DestinationAirport: slice.Segments[^1].Destination?.IataCode);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}

internal sealed record DuffelOfferRequestBody([property: JsonPropertyName("data")] DuffelOfferRequestData Data);

internal sealed record DuffelOfferRequestData(
    [property: JsonPropertyName("slices")] IReadOnlyList<DuffelSlice> Slices,
    [property: JsonPropertyName("passengers")] IReadOnlyList<DuffelPassenger> Passengers);

internal sealed record DuffelSlice(
    [property: JsonPropertyName("origin")] string Origin,
    [property: JsonPropertyName("destination")] string Destination,
    [property: JsonPropertyName("departure_date")] string DepartureDate);

internal sealed record DuffelPassenger([property: JsonPropertyName("type")] string Type);

internal sealed record DuffelOfferRequestResponse([property: JsonPropertyName("data")] DuffelOfferRequestResponseData? Data);

internal sealed record DuffelOfferRequestResponseData([property: JsonPropertyName("offers")] IReadOnlyList<DuffelOffer>? Offers);

internal sealed record DuffelOffer(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("total_amount")] string TotalAmount,
    [property: JsonPropertyName("total_currency")] string TotalCurrency,
    [property: JsonPropertyName("expires_at")] DateTimeOffset ExpiresAt,
    [property: JsonPropertyName("slices")] IReadOnlyList<DuffelOfferSlice> Slices,
    [property: JsonPropertyName("conditions")] DuffelOfferConditions? Conditions);

/// <summary>The offer-level slice -- distinct from the request-level <see cref="DuffelSlice"/> above,
/// which only echoes back origin/destination/date. This one carries what an offer actually resolved
/// to: a total <see cref="Duration"/> (an ISO 8601 duration, e.g. "PT7H30M" -- <see cref="XmlConvert.ToTimeSpan"/>
/// parses this format natively) and the flight <see cref="Segments"/> a stop count comes from.</summary>
internal sealed record DuffelOfferSlice(
    [property: JsonPropertyName("duration")] string? Duration,
    [property: JsonPropertyName("segments")] IReadOnlyList<DuffelSegment> Segments);

/// <summary><c>Id</c> is read for <see cref="IReadOnlyList{T}.Count"/> (stop count); <c>Origin</c>/
/// <c>Destination</c> for the real departure/arrival airport, both plain IATA-code strings -- safe to
/// read, unlike <c>departing_at</c>/<c>arriving_at</c>, which Duffel returns without a UTC offset and
/// would need airport-timezone data this connector doesn't have to interpret correctly.</summary>
internal sealed record DuffelSegment(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("origin")] DuffelAirport? Origin,
    [property: JsonPropertyName("destination")] DuffelAirport? Destination);

internal sealed record DuffelAirport([property: JsonPropertyName("iata_code")] string? IataCode);

internal sealed record DuffelOfferConditions(
    [property: JsonPropertyName("refund_before_departure")] DuffelPenaltyCondition? RefundBeforeDeparture);

internal sealed record DuffelPenaltyCondition([property: JsonPropertyName("allowed")] bool Allowed);
