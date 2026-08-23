using FlightAi.Core.Offers;
using FlightAi.Core.Suppliers;
using Xunit;

namespace FlightAi.Tests;

public class SupplierFanOutOrchestratorTests
{
    private sealed class FakeConnector(string id, Func<CancellationToken, Task<IReadOnlyList<Offer>>> behavior)
        : ISupplierConnector
    {
        public string SupplierId => id;
        public int CallCount { get; private set; }

        public Task<IReadOnlyList<Offer>> SearchAsync(SearchRequest request, CancellationToken cancellationToken)
        {
            CallCount++;
            return behavior(cancellationToken);
        }
    }

    private static SearchRequest SampleRequest() => new()
    {
        Origin = "AAA",
        Destination = "BBB",
        DepartureDate = new DateOnly(2026, 12, 1),
        Travellers = new TravellerCounts(1)
    };

    /// <summary>"One slow supplier stalls every search" is the failure mode this control exists to prevent.</summary>
    [Fact]
    public async Task SlowSupplier_TimesOut_WithoutFailingTheWholeSearch()
    {
        var fast = new FakeConnector("fast", async ct =>
        {
            await Task.Delay(10, ct);
            return TestOffers.One(TestOffers.Make("f1", 100m));
        });
        var slow = new FakeConnector("slow", async ct =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5), ct); // cancelled by the per-supplier timeout below
            return TestOffers.One(TestOffers.Make("s1", 100m));
        });

        var orchestrator = new SupplierFanOutOrchestrator(
            [fast, slow], new LookToBookBudget(10), perSupplierTimeout: TimeSpan.FromMilliseconds(100));

        var result = await orchestrator.SearchAsync(SampleRequest());

        Assert.True(result.IsPartial);
        Assert.Single(result.Offers);
        Assert.Equal("f1", result.Offers[0].OfferId);
    }

    [Fact]
    public async Task CircuitBreaker_OpensAfterThreshold_AndThenSkipsWithoutCallingTheConnector()
    {
        var flaky = new FakeConnector("flaky", ct => throw new InvalidOperationException("supplier down"));

        var orchestrator = new SupplierFanOutOrchestrator(
            [flaky], new LookToBookBudget(10),
            circuitFailureThreshold: 2, circuitOpenDuration: TimeSpan.FromMinutes(5));

        await orchestrator.SearchAsync(SampleRequest()); // failure 1
        await orchestrator.SearchAsync(SampleRequest()); // failure 2 -> circuit opens
        var third = await orchestrator.SearchAsync(SampleRequest());

        Assert.True(third.PerSupplier[0].CircuitOpen);
        Assert.Equal(2, flaky.CallCount); // the third call never reached the connector at all
    }

    [Fact]
    public async Task BudgetExhaustion_SkipsTheCall_WithoutInvokingTheConnector()
    {
        var connector = new FakeConnector("gds", _ => Task.FromResult(TestOffers.One(TestOffers.Make("g1", 100m))));
        var orchestrator = new SupplierFanOutOrchestrator([connector], new LookToBookBudget(1));

        await orchestrator.SearchAsync(SampleRequest());
        var second = await orchestrator.SearchAsync(SampleRequest());

        Assert.True(second.PerSupplier[0].BudgetSkipped);
        Assert.Equal(1, connector.CallCount);
    }
}
