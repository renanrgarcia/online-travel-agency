using FlightAi.Core.Models.Offers;
using FlightAi.Core.Models.Suppliers;
using FlightAi.Core.Services.Suppliers;
using Xunit;

namespace FlightAi.Tests;

/// <summary>
/// One test per eval in docs/specs/tasks/07-look-to-book-budget-and-circuit-breaker.md. The
/// time-dependent evals (E3, E6) advance a fake clock rather than sleeping, so they stay fast and
/// don't flake under load.
/// </summary>
public class BudgetAndCircuitBreakerTests
{
    private sealed class FakeClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }

    private static FakeClock NewClock() => new(new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero));

    private static SearchRequest RequestTo(string destination) =>
        new(Origin: "GRU", Destination: destination, DepartureDate: new DateOnly(2027, 3, 12), PassengerCount: 2, Language: "en");

    private static readonly SearchRequest OrdinaryRequest = RequestTo("LIS");
    private static readonly TimeSpan GenerousTimeout = TimeSpan.FromSeconds(5);

    private static SupplierReport ReportFor(FanOutResult result, string name) =>
        result.Reports.Single(r => r.SupplierName == name);

    [Fact] // E1 — baseline
    public void E1_BudgetCeilingOfThree_PermitsThreeCalls()
    {
        var budget = new LookToBookBudget(ceiling: 3, window: TimeSpan.FromMinutes(1), NewClock());

        Assert.True(budget.TryConsume());
        Assert.True(budget.TryConsume());
        Assert.True(budget.TryConsume());
    }

    [Fact] // E2 — the ceiling binds and is observable
    public void E2_FourthCallPastACeilingOfThree_IsRefusedNotThrown()
    {
        var budget = new LookToBookBudget(ceiling: 3, window: TimeSpan.FromMinutes(1), NewClock());
        for (var i = 0; i < 3; i++)
            budget.TryConsume();

        Assert.False(budget.TryConsume());
    }

    [Fact] // E3 — the ceiling is a rate, not a permanent kill
    public void E3_AfterTheWindowElapses_CallsArePermittedAgain()
    {
        var clock = NewClock();
        var budget = new LookToBookBudget(ceiling: 2, window: TimeSpan.FromMinutes(1), clock);
        budget.TryConsume();
        budget.TryConsume();
        Assert.False(budget.TryConsume());

        clock.Advance(TimeSpan.FromMinutes(1));

        Assert.True(budget.TryConsume());
    }

    [Fact] // E4 — the breaker stops wasting time on a dead supplier
    public async Task E4_TwoConsecutiveFailures_OpensTheCircuitAndSkipsTheThirdCall()
    {
        var breaker = new SupplierCircuitBreaker(failureThreshold: 2, cooldown: TimeSpan.FromMinutes(1), NewClock());
        var orchestrator = new SupplierFanOutOrchestrator(
            [new MockNdcConnector(), new MockLccConnector()], GenerousTimeout, budget: null, breaker);
        var failingRequest = RequestTo("LIS-FAIL-SEARCH-NDC");

        await orchestrator.SearchAsync(failingRequest, CancellationToken.None);
        await orchestrator.SearchAsync(failingRequest, CancellationToken.None);
        var third = await orchestrator.SearchAsync(failingRequest, CancellationToken.None);

        Assert.Equal(SupplierStatus.SkippedCircuitOpen, ReportFor(third, "NDC").Status);
    }

    [Fact] // E5 — breaker state is per connector, never global
    public async Task E5_WithOneConnectorsCircuitOpen_TheOtherIsStillInvoked()
    {
        var breaker = new SupplierCircuitBreaker(failureThreshold: 2, cooldown: TimeSpan.FromMinutes(1), NewClock());
        var orchestrator = new SupplierFanOutOrchestrator(
            [new MockNdcConnector(), new MockLccConnector()], GenerousTimeout, budget: null, breaker);
        var failingRequest = RequestTo("LIS-FAIL-SEARCH-NDC");

        await orchestrator.SearchAsync(failingRequest, CancellationToken.None);
        await orchestrator.SearchAsync(failingRequest, CancellationToken.None);
        var third = await orchestrator.SearchAsync(failingRequest, CancellationToken.None);

        Assert.Equal(SupplierStatus.Succeeded, ReportFor(third, "LCC").Status);
        Assert.Equal(2, third.Offers.Count);
    }

    [Fact] // E6 — the breaker recovers rather than permanently disabling a supplier
    public async Task E6_AfterTheCooldownElapses_TheConnectorIsInvokedAgain()
    {
        var clock = NewClock();
        var breaker = new SupplierCircuitBreaker(failureThreshold: 2, cooldown: TimeSpan.FromMinutes(1), clock);
        var orchestrator = new SupplierFanOutOrchestrator(
            [new MockNdcConnector(), new MockLccConnector()], GenerousTimeout, budget: null, breaker);
        var failingRequest = RequestTo("LIS-FAIL-SEARCH-NDC");

        await orchestrator.SearchAsync(failingRequest, CancellationToken.None);
        await orchestrator.SearchAsync(failingRequest, CancellationToken.None);
        Assert.Equal(SupplierStatus.SkippedCircuitOpen, ReportFor(await orchestrator.SearchAsync(failingRequest, CancellationToken.None), "NDC").Status);

        clock.Advance(TimeSpan.FromMinutes(1));

        // Now succeeding, so the recovered call is genuinely invoked rather than skipped.
        var afterCooldown = await orchestrator.SearchAsync(OrdinaryRequest, CancellationToken.None);
        Assert.Equal(SupplierStatus.Succeeded, ReportFor(afterCooldown, "NDC").Status);
    }

    [Fact] // E7 — the threshold counts consecutive failures; a success resets the count
    public void E7_FailureThenSuccessThenFailure_LeavesTheCircuitClosed()
    {
        var breaker = new SupplierCircuitBreaker(failureThreshold: 2, cooldown: TimeSpan.FromMinutes(1), NewClock());

        breaker.RecordFailure("NDC");
        breaker.RecordSuccess("NDC");
        breaker.RecordFailure("NDC");

        Assert.False(breaker.IsOpen("NDC"));
    }

    [Fact] // E8 — "not called" is different information from "called and failed"
    public async Task E8_CircuitOpenStatus_IsDistinctFromFailedAndFromTimedOut()
    {
        var breaker = new SupplierCircuitBreaker(failureThreshold: 1, cooldown: TimeSpan.FromMinutes(1), NewClock());
        var orchestrator = new SupplierFanOutOrchestrator(
            [new MockNdcConnector(), new MockLccConnector()], GenerousTimeout, budget: null, breaker);

        await orchestrator.SearchAsync(RequestTo("LIS-FAIL-SEARCH-NDC"), CancellationToken.None);
        var second = await orchestrator.SearchAsync(RequestTo("LIS-FAIL-SEARCH-LCC"), CancellationToken.None);

        var ndc = ReportFor(second, "NDC");
        var lcc = ReportFor(second, "LCC");

        Assert.Equal(SupplierStatus.SkippedCircuitOpen, ndc.Status);
        Assert.Equal(SupplierStatus.Failed, lcc.Status);
        Assert.NotEqual(SupplierStatus.Failed, ndc.Status);
        Assert.NotEqual(SupplierStatus.TimedOut, ndc.Status);
    }

    [Fact] // E9 — a supplier that always times out is as dead as one that errors
    public async Task E9_TimeoutsCountTowardTheBreakersFailureTally()
    {
        var breaker = new SupplierCircuitBreaker(failureThreshold: 2, cooldown: TimeSpan.FromMinutes(1), NewClock());
        var orchestrator = new SupplierFanOutOrchestrator(
            [new MockNdcConnector(simulatedDelay: TimeSpan.FromSeconds(10)), new MockLccConnector()],
            perConnectorTimeout: TimeSpan.FromMilliseconds(150), budget: null, breaker);

        await orchestrator.SearchAsync(OrdinaryRequest, CancellationToken.None);
        await orchestrator.SearchAsync(OrdinaryRequest, CancellationToken.None);
        var third = await orchestrator.SearchAsync(OrdinaryRequest, CancellationToken.None);

        Assert.Equal(SupplierStatus.SkippedCircuitOpen, ReportFor(third, "NDC").Status);
    }

    [Fact] // E10 — partial results still beat none
    public async Task E10_BudgetExhaustedMidFanOut_SkipsTheRemainingConnectorButKeepsTheRest()
    {
        var budget = new LookToBookBudget(ceiling: 1, window: TimeSpan.FromMinutes(1), NewClock());
        var orchestrator = new SupplierFanOutOrchestrator(
            [new MockNdcConnector(), new MockLccConnector()], GenerousTimeout, budget, breaker: null);

        var result = await orchestrator.SearchAsync(OrdinaryRequest, CancellationToken.None);

        var statuses = result.Reports.Select(r => r.Status).ToList();
        Assert.Contains(SupplierStatus.Succeeded, statuses);
        Assert.Contains(SupplierStatus.SkippedBudgetExhausted, statuses);
        Assert.Equal(2, result.Offers.Count); // exactly the one connector that got budget
    }

    [Fact] // E11 — integration check across tasks 04-07
    public async Task E11_HealthyConnectorPlusFlappingOnePlusTightBudget_StillReturnsUsableOffers()
    {
        var clock = NewClock();
        // Ceiling 5 is genuinely tight: the two setup searches consume 4 (both connectors each time),
        // leaving exactly one call for the third search -- which the open circuit hands to LCC.
        var budget = new LookToBookBudget(ceiling: 5, window: TimeSpan.FromMinutes(1), clock);
        var breaker = new SupplierCircuitBreaker(failureThreshold: 2, cooldown: TimeSpan.FromMinutes(1), clock);
        var orchestrator = new SupplierFanOutOrchestrator(
            [new MockNdcConnector(), new MockLccConnector()], GenerousTimeout, budget, breaker);

        // NDC flaps: fails twice, opening its circuit. LCC stays healthy throughout.
        await orchestrator.SearchAsync(RequestTo("LIS-FAIL-SEARCH-NDC"), CancellationToken.None);
        await orchestrator.SearchAsync(RequestTo("LIS-FAIL-SEARCH-NDC"), CancellationToken.None);
        var result = await orchestrator.SearchAsync(OrdinaryRequest, CancellationToken.None);

        Assert.NotEmpty(result.Offers);
        Assert.All(result.Offers, offer => Assert.StartsWith("LCC-", offer.OfferId));
        Assert.Equal(SupplierStatus.SkippedCircuitOpen, ReportFor(result, "NDC").Status);
        Assert.Equal(SupplierStatus.Succeeded, ReportFor(result, "LCC").Status);
    }
}
