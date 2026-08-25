using System.Diagnostics;
using FlightAi.Core.Interfaces.Suppliers;
using FlightAi.Core.Models.Offers;
using FlightAi.Core.Models.Suppliers;
using FlightAi.Core.Services.Suppliers;
using Xunit;

namespace FlightAi.Tests;

/// <summary>
/// One test per eval in docs/specs/tasks/06-supplier-fan-out-orchestrator.md. Every connector needs an
/// entry in the policies dictionary (task 07's SupplierPolicy) even when this task doesn't care about
/// budget/breaker -- <see cref="PoliciesFor"/> builds a plain timeout-only policy per connector name.
/// </summary>
public class SupplierFanOutOrchestratorTests
{
    private static SearchRequest RequestTo(string destination) =>
        new(Origin: "GRU", Destination: destination, DepartureDate: new DateOnly(2027, 3, 12), PassengerCount: 2, Language: "en");

    private static readonly SearchRequest OrdinaryRequest = RequestTo("LIS");
    private static readonly TimeSpan GenerousTimeout = TimeSpan.FromSeconds(5);

    private static Dictionary<string, SupplierPolicy> PoliciesFor(TimeSpan timeout, params string[] connectorNames) =>
        connectorNames.ToDictionary(name => name, _ => SupplierPolicy.WithNoLimits(timeout));

    /// <summary>Deliberately violates task 04's "failures are returned, not thrown" contract, so E2's
    /// "no exception escapes" is tested against a genuinely misbehaving connector.</summary>
    private sealed class ThrowingConnector : ISupplierConnector
    {
        public string Name => "THROWS";

        public Task<SupplierSearchResult> SearchAsync(SearchRequest request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("connector blew up");
    }

    private static SupplierReport ReportFor(FanOutResult result, string name) =>
        result.Reports.Single(r => r.SupplierName == name);

    [Fact] // E1 — baseline
    public async Task E1_TwoHealthyConnectors_ReturnsAllOffersAndReportsBothSucceeded()
    {
        var orchestrator = new SupplierFanOutOrchestrator(
            [new MockNdcConnector(), new MockLccConnector()], PoliciesFor(GenerousTimeout, "NDC", "LCC"));

        var result = await orchestrator.SearchAsync(OrdinaryRequest, CancellationToken.None);

        Assert.Equal(4, result.Offers.Count);
        Assert.Equal(SupplierStatus.Succeeded, ReportFor(result, "NDC").Status);
        Assert.Equal(SupplierStatus.Succeeded, ReportFor(result, "LCC").Status);
    }

    [Fact] // E2 — the degradation guarantee
    public async Task E2_OneHealthyOneFailing_ReturnsHealthyOffersAndReportsTheFailure()
    {
        var orchestrator = new SupplierFanOutOrchestrator(
            [new MockNdcConnector(), new MockLccConnector()], PoliciesFor(GenerousTimeout, "NDC", "LCC"));

        var result = await orchestrator.SearchAsync(RequestTo("LIS-FAIL-SEARCH-NDC"), CancellationToken.None);

        Assert.Equal(2, result.Offers.Count);
        Assert.All(result.Offers, offer => Assert.StartsWith("LCC-", offer.OfferId));

        var failed = ReportFor(result, "NDC");
        Assert.Equal(SupplierStatus.Failed, failed.Status);
        Assert.NotNull(failed.Reason);
    }

    [Fact] // E2 — same guarantee against a connector that throws instead of returning a failure
    public async Task E2_ConnectorThatThrows_IsCaughtAndReportedRatherThanEscaping()
    {
        var orchestrator = new SupplierFanOutOrchestrator(
            [new ThrowingConnector(), new MockLccConnector()], PoliciesFor(GenerousTimeout, "THROWS", "LCC"));

        var result = await orchestrator.SearchAsync(OrdinaryRequest, CancellationToken.None);

        Assert.Equal(2, result.Offers.Count);
        var thrown = ReportFor(result, "THROWS");
        Assert.Equal(SupplierStatus.Failed, thrown.Status);
        Assert.Contains("blew up", thrown.Reason);
    }

    [Fact] // E3 — the timeout actually bounds latency
    public async Task E3_ConnectorHangingPastTheTimeout_IsCutOffAndReportedTimedOut()
    {
        var orchestrator = new SupplierFanOutOrchestrator(
            [new MockNdcConnector(simulatedDelay: TimeSpan.FromSeconds(10)), new MockLccConnector()],
            PoliciesFor(TimeSpan.FromMilliseconds(200), "NDC", "LCC"));
        var stopwatch = Stopwatch.StartNew();

        var result = await orchestrator.SearchAsync(OrdinaryRequest, CancellationToken.None);
        stopwatch.Stop();

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2), $"expected the timeout to bound this, took {stopwatch.Elapsed}");
        Assert.Equal(SupplierStatus.TimedOut, ReportFor(result, "NDC").Status);
        Assert.Equal(2, result.Offers.Count); // the healthy connector still contributed
    }

    [Fact] // E4 — proves genuine parallelism, invisible without timing
    public async Task E4_TwoDelayedConnectors_RunConcurrentlyNotSequentially()
    {
        var delay = TimeSpan.FromMilliseconds(300);
        var orchestrator = new SupplierFanOutOrchestrator(
            [new MockNdcConnector(delay), new MockLccConnector(delay)],
            PoliciesFor(TimeSpan.FromSeconds(1), "NDC", "LCC"));
        var stopwatch = Stopwatch.StartNew();

        var result = await orchestrator.SearchAsync(OrdinaryRequest, CancellationToken.None);
        stopwatch.Stop();

        Assert.Equal(4, result.Offers.Count);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromMilliseconds(550),
            $"sequential execution would take ~600ms; took {stopwatch.Elapsed}");
    }

    [Fact] // E5 — misattributing a timeout as supplier fault would poison task 07's breaker
    public async Task E5_TimedOutStatus_IsDistinctFromSupplierReportedFailure()
    {
        var orchestrator = new SupplierFanOutOrchestrator(
            [new MockNdcConnector(simulatedDelay: TimeSpan.FromSeconds(10)), new MockLccConnector()],
            PoliciesFor(TimeSpan.FromMilliseconds(200), "NDC", "LCC"));

        var result = await orchestrator.SearchAsync(RequestTo("LIS-FAIL-SEARCH-LCC"), CancellationToken.None);

        Assert.Equal(SupplierStatus.TimedOut, ReportFor(result, "NDC").Status);
        Assert.Equal(SupplierStatus.Failed, ReportFor(result, "LCC").Status);
        Assert.NotEqual(ReportFor(result, "NDC").Status, ReportFor(result, "LCC").Status);
    }

    [Fact] // E6 — "everything failed" is still a valid answer the API must be able to stream
    public async Task E6_AllConnectorsFail_ReturnsEmptyOffersSuccessfullyWithAllReportedFailed()
    {
        var orchestrator = new SupplierFanOutOrchestrator(
            [new MockNdcConnector(), new MockLccConnector()], PoliciesFor(GenerousTimeout, "NDC", "LCC"));

        var result = await orchestrator.SearchAsync(RequestTo("FAIL-SEARCH-NDC-FAIL-SEARCH-LCC"), CancellationToken.None);

        Assert.Empty(result.Offers);
        Assert.Equal(2, result.Reports.Count);
        Assert.All(result.Reports, report => Assert.Equal(SupplierStatus.Failed, report.Status));
    }

    [Fact] // E7 — degenerate case
    public async Task E7_ZeroConnectorsRegistered_ReturnsEmptyResultWithoutThrowing()
    {
        var orchestrator = new SupplierFanOutOrchestrator([], PoliciesFor(GenerousTimeout));

        var result = await orchestrator.SearchAsync(OrdinaryRequest, CancellationToken.None);

        Assert.Empty(result.Offers);
        Assert.Empty(result.Reports);
    }

    [Fact] // E8 — task 13 emits one supplier-result per connector; a gap or duplicate is client-visible
    public async Task E8_EveryRegisteredConnector_AppearsExactlyOnceInTheReport()
    {
        var orchestrator = new SupplierFanOutOrchestrator(
            [new MockNdcConnector(), new MockLccConnector(), new ThrowingConnector()],
            PoliciesFor(GenerousTimeout, "NDC", "LCC", "THROWS"));

        var result = await orchestrator.SearchAsync(OrdinaryRequest, CancellationToken.None);

        Assert.Equal(3, result.Reports.Count);
        Assert.Equal(["NDC", "LCC", "THROWS"], result.Reports.Select(r => r.SupplierName));
    }

    [Fact] // E9 — task 03's ranking must receive a stable input
    public async Task E9_MergedOffers_HaveNoIdCollisionsAndADeterministicOrder()
    {
        var orchestrator = new SupplierFanOutOrchestrator(
            [new MockNdcConnector(), new MockLccConnector()], PoliciesFor(GenerousTimeout, "NDC", "LCC"));

        var first = await orchestrator.SearchAsync(OrdinaryRequest, CancellationToken.None);
        var second = await orchestrator.SearchAsync(OrdinaryRequest, CancellationToken.None);

        var ids = first.Offers.Select(o => o.OfferId).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.Equal(ids, second.Offers.Select(o => o.OfferId));
        Assert.Equal(["NDC-001", "NDC-002", "LCC-001", "LCC-002"], ids); // registration order, then offer ID
    }

    [Fact] // a connector missing from the policies dictionary fails fast rather than silently misbehaving
    public void ConnectorWithNoRegisteredPolicy_FailsFastAtConstructionRatherThanBeingSilentlyMisconfigured()
    {
        // LCC has no entry on purpose. Policies are consulted while building each connector's
        // budget/breaker at construction time, so a missing one is caught before any search ever
        // runs -- not discovered mid-fan-out.
        Assert.Throws<ArgumentException>(() => new SupplierFanOutOrchestrator(
            [new MockNdcConnector(), new MockLccConnector()], PoliciesFor(GenerousTimeout, "NDC")));
    }
}
