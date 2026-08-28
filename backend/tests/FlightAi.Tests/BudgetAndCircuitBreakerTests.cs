using FlightAi.Core.Models.Offers;
using FlightAi.Core.Models.Suppliers;
using FlightAi.Core.Services.Suppliers;
using Xunit;

namespace FlightAi.Tests;

/// <summary>
/// One test per eval in docs/features/01-backend/tasks/07-look-to-book-budget-and-circuit-breaker.md. The
/// time-dependent evals (E3, E6) advance a fake clock rather than sleeping, so they stay fast and
/// don't flake under load.
/// <para>
/// Budget and breaker are configured per connector via <see cref="SupplierPolicy"/>, not shared across
/// every connector -- several tests below (E4, E9, E11) deliberately give NDC and LCC *different*
/// policies to demonstrate that directly, which a single shared instance could never have shown.
/// </para>
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

    /// <summary>Drains <see cref="SupplierFanOutOrchestrator.SearchStreamingAsync"/> into one aggregate --
    /// there's no production <c>SearchAsync</c> anymore.</summary>
    private static async Task<FanOutResult> CollectAsync(
        IAsyncEnumerable<(IReadOnlyList<Offer> Offers, SupplierReport Report)> stream)
    {
        var offers = new List<Offer>();
        var reports = new List<SupplierReport>();
        await foreach (var (outcomeOffers, report) in stream)
        {
            offers.AddRange(outcomeOffers);
            reports.Add(report);
        }
        return new FanOutResult(offers, reports);
    }

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
        var clock = NewClock();
        var policies = new Dictionary<string, SupplierPolicy>
        {
            // NDC gets a breaker; LCC deliberately doesn't -- proving the two connectors' policies
            // are genuinely independent, not just two names pointing at one shared instance.
            ["NDC"] = SupplierPolicy.WithNoLimits(GenerousTimeout) with { BreakerFailureThreshold = 2, BreakerCooldown = TimeSpan.FromMinutes(1) },
            ["LCC"] = SupplierPolicy.WithNoLimits(GenerousTimeout),
        };
        var orchestrator = new SupplierFanOutOrchestrator([new MockNdcConnector(), new MockLccConnector()], policies, clock);
        var failingRequest = RequestTo("LIS-FAIL-SEARCH-NDC");

        await CollectAsync(orchestrator.SearchStreamingAsync(failingRequest, CancellationToken.None));
        await CollectAsync(orchestrator.SearchStreamingAsync(failingRequest, CancellationToken.None));
        var third = await CollectAsync(orchestrator.SearchStreamingAsync(failingRequest, CancellationToken.None));

        Assert.Equal(SupplierStatus.SkippedCircuitOpen, ReportFor(third, "NDC").Status);
    }

    [Fact] // E5 — breaker state is per connector, never global
    public async Task E5_WithOneConnectorsCircuitOpen_TheOtherIsStillInvoked()
    {
        var clock = NewClock();
        var policies = new Dictionary<string, SupplierPolicy>
        {
            ["NDC"] = SupplierPolicy.WithNoLimits(GenerousTimeout) with { BreakerFailureThreshold = 2, BreakerCooldown = TimeSpan.FromMinutes(1) },
            ["LCC"] = SupplierPolicy.WithNoLimits(GenerousTimeout),
        };
        var orchestrator = new SupplierFanOutOrchestrator([new MockNdcConnector(), new MockLccConnector()], policies, clock);
        var failingRequest = RequestTo("LIS-FAIL-SEARCH-NDC");

        await CollectAsync(orchestrator.SearchStreamingAsync(failingRequest, CancellationToken.None));
        await CollectAsync(orchestrator.SearchStreamingAsync(failingRequest, CancellationToken.None));
        var third = await CollectAsync(orchestrator.SearchStreamingAsync(failingRequest, CancellationToken.None));

        Assert.Equal(SupplierStatus.Succeeded, ReportFor(third, "LCC").Status);
        Assert.Equal(2, third.Offers.Count);
    }

    [Fact] // E6 — the breaker recovers rather than permanently disabling a supplier
    public async Task E6_AfterTheCooldownElapses_TheConnectorIsInvokedAgain()
    {
        var clock = NewClock();
        var policies = new Dictionary<string, SupplierPolicy>
        {
            ["NDC"] = SupplierPolicy.WithNoLimits(GenerousTimeout) with { BreakerFailureThreshold = 2, BreakerCooldown = TimeSpan.FromMinutes(1) },
            ["LCC"] = SupplierPolicy.WithNoLimits(GenerousTimeout),
        };
        var orchestrator = new SupplierFanOutOrchestrator([new MockNdcConnector(), new MockLccConnector()], policies, clock);
        var failingRequest = RequestTo("LIS-FAIL-SEARCH-NDC");

        await CollectAsync(orchestrator.SearchStreamingAsync(failingRequest, CancellationToken.None));
        await CollectAsync(orchestrator.SearchStreamingAsync(failingRequest, CancellationToken.None));
        Assert.Equal(SupplierStatus.SkippedCircuitOpen, ReportFor(await CollectAsync(orchestrator.SearchStreamingAsync(failingRequest, CancellationToken.None)), "NDC").Status);

        clock.Advance(TimeSpan.FromMinutes(1));

        // Now succeeding, so the recovered call is genuinely invoked rather than skipped.
        var afterCooldown = await CollectAsync(orchestrator.SearchStreamingAsync(OrdinaryRequest, CancellationToken.None));
        Assert.Equal(SupplierStatus.Succeeded, ReportFor(afterCooldown, "NDC").Status);
    }

    [Fact] // E7 — the threshold counts consecutive failures; a success resets the count
    public void E7_FailureThenSuccessThenFailure_LeavesTheCircuitClosed()
    {
        var breaker = new SupplierCircuitBreaker(failureThreshold: 2, cooldown: TimeSpan.FromMinutes(1), NewClock());

        breaker.RecordFailure();
        breaker.RecordSuccess();
        breaker.RecordFailure();

        Assert.False(breaker.IsOpen);
    }

    [Fact] // E8 — "not called" is different information from "called and failed"
    public async Task E8_CircuitOpenStatus_IsDistinctFromFailedAndFromTimedOut()
    {
        var clock = NewClock();
        var policies = new Dictionary<string, SupplierPolicy>
        {
            ["NDC"] = SupplierPolicy.WithNoLimits(GenerousTimeout) with { BreakerFailureThreshold = 1, BreakerCooldown = TimeSpan.FromMinutes(1) },
            ["LCC"] = SupplierPolicy.WithNoLimits(GenerousTimeout),
        };
        var orchestrator = new SupplierFanOutOrchestrator([new MockNdcConnector(), new MockLccConnector()], policies, clock);

        await CollectAsync(orchestrator.SearchStreamingAsync(RequestTo("LIS-FAIL-SEARCH-NDC"), CancellationToken.None));
        var second = await CollectAsync(orchestrator.SearchStreamingAsync(RequestTo("LIS-FAIL-SEARCH-LCC"), CancellationToken.None));

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
        var clock = NewClock();
        // NDC gets a strict timeout paired with its breaker; LCC gets a generous timeout and no
        // breaker at all -- exactly the kind of heterogeneous, per-supplier tuning a shared
        // timeout/breaker could never express.
        var policies = new Dictionary<string, SupplierPolicy>
        {
            ["NDC"] = SupplierPolicy.WithNoLimits(TimeSpan.FromMilliseconds(150)) with { BreakerFailureThreshold = 2, BreakerCooldown = TimeSpan.FromMinutes(1) },
            ["LCC"] = SupplierPolicy.WithNoLimits(GenerousTimeout),
        };
        var orchestrator = new SupplierFanOutOrchestrator(
            [new MockNdcConnector(simulatedDelay: TimeSpan.FromSeconds(10)), new MockLccConnector()], policies, clock);

        await CollectAsync(orchestrator.SearchStreamingAsync(OrdinaryRequest, CancellationToken.None));
        await CollectAsync(orchestrator.SearchStreamingAsync(OrdinaryRequest, CancellationToken.None));
        var third = await CollectAsync(orchestrator.SearchStreamingAsync(OrdinaryRequest, CancellationToken.None));

        Assert.Equal(SupplierStatus.SkippedCircuitOpen, ReportFor(third, "NDC").Status);
    }

    [Fact] // E10 — partial results still beat none
    public async Task E10_BudgetExhaustedForOneConnector_SkipsOnlyThatConnectorOnASubsequentSearch()
    {
        // Per-connector budgets don't compete for one shared pool, so "exhausted" now means "this
        // connector's own ceiling, reached across repeated searches" rather than "two connectors
        // raced for the same slot in a single search" -- the latter scenario no longer exists by
        // construction, which is the whole point of the fix.
        var clock = NewClock();
        var policies = new Dictionary<string, SupplierPolicy>
        {
            ["NDC"] = SupplierPolicy.WithNoLimits(GenerousTimeout) with { BudgetCeiling = 1, BudgetWindow = TimeSpan.FromMinutes(1) },
            ["LCC"] = SupplierPolicy.WithNoLimits(GenerousTimeout),
        };
        var orchestrator = new SupplierFanOutOrchestrator([new MockNdcConnector(), new MockLccConnector()], policies, clock);

        await CollectAsync(orchestrator.SearchStreamingAsync(OrdinaryRequest, CancellationToken.None)); // consumes NDC's only slot
        var second = await CollectAsync(orchestrator.SearchStreamingAsync(OrdinaryRequest, CancellationToken.None));

        Assert.Equal(SupplierStatus.SkippedBudgetExhausted, ReportFor(second, "NDC").Status);
        Assert.Equal(SupplierStatus.Succeeded, ReportFor(second, "LCC").Status);
        Assert.Equal(2, second.Offers.Count); // exactly LCC's offers
    }

    [Fact] // E11 — integration check across tasks 04-07
    public async Task E11_HealthyConnectorPlusFlappingOnePlusPerConnectorLimits_StillReturnsUsableOffers()
    {
        var clock = NewClock();
        var policies = new Dictionary<string, SupplierPolicy>
        {
            ["NDC"] = SupplierPolicy.WithNoLimits(GenerousTimeout) with
            {
                BudgetCeiling = 10, BudgetWindow = TimeSpan.FromMinutes(1),
                BreakerFailureThreshold = 2, BreakerCooldown = TimeSpan.FromMinutes(1),
            },
            ["LCC"] = SupplierPolicy.WithNoLimits(GenerousTimeout) with { BudgetCeiling = 10, BudgetWindow = TimeSpan.FromMinutes(1) },
        };
        var orchestrator = new SupplierFanOutOrchestrator([new MockNdcConnector(), new MockLccConnector()], policies, clock);

        // NDC flaps: fails twice, opening its circuit. LCC stays healthy throughout, under its own budget.
        await CollectAsync(orchestrator.SearchStreamingAsync(RequestTo("LIS-FAIL-SEARCH-NDC"), CancellationToken.None));
        await CollectAsync(orchestrator.SearchStreamingAsync(RequestTo("LIS-FAIL-SEARCH-NDC"), CancellationToken.None));
        var result = await CollectAsync(orchestrator.SearchStreamingAsync(OrdinaryRequest, CancellationToken.None));

        Assert.NotEmpty(result.Offers);
        Assert.All(result.Offers, offer => Assert.StartsWith("LCC-", offer.OfferId));
        Assert.Equal(SupplierStatus.SkippedCircuitOpen, ReportFor(result, "NDC").Status);
        Assert.Equal(SupplierStatus.Succeeded, ReportFor(result, "LCC").Status);
    }
}
