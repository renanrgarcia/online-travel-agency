using System.Diagnostics;
using FlightAi.Core.Models;
using FlightAi.Core.Services;
using Xunit;

namespace FlightAi.Tests;

/// <summary>
/// One test per eval in docs/specs/tasks/05-mock-supplier-connectors.md.
/// </summary>
public class MockSupplierConnectorTests
{
    private static SearchRequest RequestTo(string destination) =>
        new(Origin: "GRU", Destination: destination, DepartureDate: new DateOnly(2027, 3, 12), PassengerCount: 2, Language: "en");

    private static readonly SearchRequest OrdinaryRequest = RequestTo("LIS");

    [Fact] // E1 — reproducibility is the reason these exist
    public async Task E1_OrdinaryRequest_RunTwice_ReturnsByteIdenticalOfferSets()
    {
        var connector = new MockNdcConnector();

        var first = await connector.SearchAsync(OrdinaryRequest, CancellationToken.None);
        var second = await connector.SearchAsync(OrdinaryRequest, CancellationToken.None);

        Assert.Equal(first.Offers, second.Offers);
    }

    [Fact] // E2 — task 06 merges these; colliding IDs would corrupt task 01's per-offer tokens
    public async Task E2_DifferentConnectors_ReturnDifferentOffersWithNoIdCollisions()
    {
        var gds = await new MockGdsConnector().SearchAsync(OrdinaryRequest, CancellationToken.None);
        var ndc = await new MockNdcConnector().SearchAsync(OrdinaryRequest, CancellationToken.None);
        var lcc = await new MockLccConnector().SearchAsync(OrdinaryRequest, CancellationToken.None);

        var allIds = gds.Offers.Concat(ndc.Offers).Concat(lcc.Offers).Select(o => o.OfferId).ToList();

        Assert.NotEqual(gds.Offers, ndc.Offers);
        Assert.NotEqual(ndc.Offers, lcc.Offers);
        Assert.Equal(allIds.Count, allIds.Distinct().Count());
    }

    [Fact] // E3 — task 04 E3, exercised for real
    public async Task E3_RequestCarryingFailMarker_ReturnsFailureWithReasonAndNoException()
    {
        var connector = new MockNdcConnector();
        var request = RequestTo("LIS-FAIL-SEARCH-NDC");

        var result = await connector.SearchAsync(request, CancellationToken.None);

        Assert.Equal(SupplierOutcome.Failure, result.Outcome);
        Assert.NotNull(result.FailureReason);
        Assert.Empty(result.Offers);
    }

    [Fact] // E4 — failure is per-connector, never global
    public async Task E4_RequestFailingOneConnector_StillSucceedsOnTheOther()
    {
        var request = RequestTo("LIS-FAIL-SEARCH-NDC");

        var ndcResult = await new MockNdcConnector().SearchAsync(request, CancellationToken.None);
        var lccResult = await new MockLccConnector().SearchAsync(request, CancellationToken.None);

        Assert.Equal(SupplierOutcome.Failure, ndcResult.Outcome);
        Assert.Equal(SupplierOutcome.Success, lccResult.Outcome);
        Assert.NotEmpty(lccResult.Offers);
    }

    [Fact] // E5 — task 06's timeout depends on cancellation actually being honoured, not merely accepted
    public async Task E5_CancelledPartwayThroughADelay_ReturnsPromptlyAsCancelled()
    {
        var connector = new MockNdcConnector(simulatedDelay: TimeSpan.FromSeconds(5));
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var stopwatch = Stopwatch.StartNew();

        var result = await connector.SearchAsync(OrdinaryRequest, cts.Token);
        stopwatch.Stop();

        Assert.Equal(SupplierOutcome.Cancelled, result.Outcome);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1), $"expected a prompt cancellation, took {stopwatch.Elapsed}");
    }

    [Fact] // E6 — feeds tasks 06-08 without surprises
    public async Task E6_EveryOffer_HasAUniqueStableIdAcrossAllConnectors()
    {
        var allOffers = await AllOffersAsync();

        Assert.NotEmpty(allOffers);
        Assert.Equal(allOffers.Count, allOffers.Select(o => o.OfferId).Distinct().Count());
        Assert.All(allOffers, o => Assert.False(string.IsNullOrWhiteSpace(o.OfferId)));
    }

    [Fact] // E7 — a demo where every offer scores alike proves nothing in task 08
    public async Task E7_OfferPrices_VaryEnoughThatDifferentWeightsProduceDifferentOrderings()
    {
        var allOffers = await AllOffersAsync();

        var byPrice = allOffers.OrderBy(o => o.Price).Select(o => o.OfferId).ToList();
        var byDuration = allOffers.OrderBy(o => o.Duration).Select(o => o.OfferId).ToList();

        Assert.NotEqual(byPrice, byDuration);
    }

    private static async Task<List<Offer>> AllOffersAsync()
    {
        var gds = await new MockGdsConnector().SearchAsync(OrdinaryRequest, CancellationToken.None);
        var ndc = await new MockNdcConnector().SearchAsync(OrdinaryRequest, CancellationToken.None);
        var lcc = await new MockLccConnector().SearchAsync(OrdinaryRequest, CancellationToken.None);
        return [.. gds.Offers, .. ndc.Offers, .. lcc.Offers];
    }
}
