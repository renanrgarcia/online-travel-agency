using FlightAi.Core.Suppliers;
using Xunit;

namespace FlightAi.Tests;

public class LookToBookBudgetTests
{
    [Fact]
    public void ReserveCall_ThrowsOnceTheLimitIsReached()
    {
        var budget = new LookToBookBudget(maxCallsPerSupplierPerSession: 2);

        budget.ReserveCall("gds");
        budget.ReserveCall("gds");

        Assert.Throws<LookToBookBudgetExceededException>(() => budget.ReserveCall("gds"));
    }

    [Fact]
    public void Budgets_AreTrackedIndependently_PerSupplier()
    {
        var budget = new LookToBookBudget(maxCallsPerSupplierPerSession: 1);

        budget.ReserveCall("gds");
        budget.ReserveCall("ndc"); // different supplier — its own counter, must not throw

        Assert.Throws<LookToBookBudgetExceededException>(() => budget.ReserveCall("gds"));
        Assert.Throws<LookToBookBudgetExceededException>(() => budget.ReserveCall("ndc"));
    }

    [Fact]
    public void TotalCalls_SumsAcrossAllSuppliers()
    {
        var budget = new LookToBookBudget(maxCallsPerSupplierPerSession: 5);

        budget.ReserveCall("gds");
        budget.ReserveCall("gds");
        budget.ReserveCall("ndc");

        Assert.Equal(3, budget.TotalCalls());
    }
}
