using System.Reflection;
using FlightAi.Core.Interfaces;
using FlightAi.Core.Models;
using FlightAi.Core.Services;
using Xunit;

namespace FlightAi.Tests;

/// <summary>
/// One test per eval in docs/specs/tasks/04-supplier-connector-interface.md. Task 04 is mostly a
/// contract, so these evals prove the contract's expressiveness against a throwaway test double
/// rather than against a real connector (task 05).
/// </summary>
public class SupplierConnectorContractTests
{
    private sealed class TestConnector(string name, Func<CancellationToken, Task<SupplierSearchResult>> handler)
        : ISupplierConnector
    {
        public string Name { get; } = name;

        public Task<SupplierSearchResult> SearchAsync(SearchRequest request, CancellationToken cancellationToken) =>
            handler(cancellationToken);
    }

    private static readonly SearchRequest AnyRequest = new(
        Origin: "GRU", Destination: "LIS", DepartureDate: new DateOnly(2027, 3, 12), PassengerCount: 2, Language: "en");

    private static Offer AnyOffer(string id) =>
        new(OfferId: id, Price: 500m, Currency: "USD", Duration: TimeSpan.FromHours(5), Stops: 0, Refundable: true, Margin: 20m,
            ExpiresAt: new DateTimeOffset(2027, 3, 1, 0, 20, 0, TimeSpan.Zero));

    [Fact] // E1 — the baseline
    public async Task E1_FullSuccess_CarriesAllOffersAndNoFailureReason()
    {
        var connector = new TestConnector("NDC", _ => Task.FromResult(
            SupplierSearchResult.Success([AnyOffer("A"), AnyOffer("B")])));

        var result = await connector.SearchAsync(AnyRequest, CancellationToken.None);

        Assert.Equal(SupplierOutcome.Success, result.Outcome);
        Assert.Equal(2, result.Offers.Count);
        Assert.Null(result.FailureReason);
    }

    [Fact] // E2 — "no flights" is a valid answer, not an error
    public async Task E2_SuccessWithZeroOffers_IsStillSuccessNotFailure()
    {
        var connector = new TestConnector("NDC", _ => Task.FromResult(
            SupplierSearchResult.Success([])));

        var result = await connector.SearchAsync(AnyRequest, CancellationToken.None);

        Assert.Equal(SupplierOutcome.Success, result.Outcome);
        Assert.Empty(result.Offers);
        Assert.NotEqual(SupplierOutcome.Failure, result.Outcome);
    }

    [Fact] // E3 — task 06 needs the reason to report per-supplier status
    public async Task E3_OutrightFailure_CarriesReasonAndZeroOffers()
    {
        var connector = new TestConnector("LCC", _ => Task.FromResult(
            SupplierSearchResult.Failure("upstream returned HTTP 503")));

        var result = await connector.SearchAsync(AnyRequest, CancellationToken.None);

        Assert.Equal(SupplierOutcome.Failure, result.Outcome);
        Assert.Empty(result.Offers);
        Assert.Equal("upstream returned HTTP 503", result.FailureReason);
    }

    [Fact] // E4 — the case most easily designed out by accident
    public async Task E4_PartialSuccess_CarriesBothTheOffersAndTheReason()
    {
        var connector = new TestConnector("GDS", _ => Task.FromResult(
            SupplierSearchResult.PartialSuccess([AnyOffer("A")], "page 2 of 3 timed out")));

        var result = await connector.SearchAsync(AnyRequest, CancellationToken.None);

        Assert.Equal(SupplierOutcome.PartialSuccess, result.Outcome);
        Assert.Single(result.Offers);
        Assert.Equal("page 2 of 3 timed out", result.FailureReason);
    }

    [Fact] // E5 — a timeout must never be misattributed to the supplier
    public async Task E5_Cancellation_IsDistinguishableFromFailure()
    {
        using var cts = new CancellationTokenSource();
        var connector = new TestConnector("NDC", ct =>
        {
            if (ct.IsCancellationRequested)
                return Task.FromResult(SupplierSearchResult.Cancelled());
            throw new InvalidOperationException("test setup expects cancellation to already be requested");
        });
        cts.Cancel();

        var result = await connector.SearchAsync(AnyRequest, cts.Token);

        Assert.Equal(SupplierOutcome.Cancelled, result.Outcome);
        Assert.NotEqual(SupplierOutcome.Failure, result.Outcome);
        Assert.Null(result.FailureReason);
    }

    [Fact] // E6 — exceptions as control flow would make task 06's degradation logic unreadable
    public void E6_EveryLegitimateState_IsConstructedWithoutThrowing()
    {
        var exception = Record.Exception(() =>
        {
            _ = SupplierSearchResult.Success([]);
            _ = SupplierSearchResult.Success([AnyOffer("A")]);
            _ = SupplierSearchResult.PartialSuccess([AnyOffer("A")], "partial");
            _ = SupplierSearchResult.Failure("failed");
            _ = SupplierSearchResult.Cancelled();
        });

        Assert.Null(exception);
    }

    [Fact] // Guards the E4/E3 boundary: PartialSuccess with zero offers is not a legitimate state
    public void PartialSuccess_WithZeroOffers_IsRejectedAsProgrammerError()
    {
        Assert.Throws<ArgumentException>(() => SupplierSearchResult.PartialSuccess([], "no offers but still partial?"));
    }

    [Fact] // E7 — prevents discovering a missing field three tasks later
    public void E7_Offer_CarriesEveryFieldOfferScorerReadsAndPriceReferenceStoreRegisters()
    {
        var offerFields = typeof(Offer).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToHashSet();

        // OfferScorer (task 03) reads these directly off ScorableOffer.
        var scorableOfferFields = typeof(ScorableOffer).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name);
        foreach (var field in scorableOfferFields)
            Assert.Contains(field, offerFields);

        // PriceReferenceStore (task 01) additionally needs these to register price/currency and
        // refundable status, which ScorableOffer never carried.
        Assert.Contains("Currency", offerFields);
        Assert.Contains("Refundable", offerFields);
    }

    [Fact] // E8 — a real offer without an expiry is an omission, added after the fact once noticed
    public void E8_Offer_CarriesAnExpiresAtOfTypeDateTimeOffset()
    {
        var property = typeof(Offer).GetProperty("ExpiresAt", BindingFlags.Public | BindingFlags.Instance);

        Assert.NotNull(property);
        Assert.Equal(typeof(DateTimeOffset), property.PropertyType);
    }
}
