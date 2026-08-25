namespace FlightAi.Core.Models.Suppliers;

/// <summary>
/// One connector's operating parameters: how long to wait, and — optionally — its own look-to-book
/// budget and circuit-breaker thresholds. Real suppliers carry genuinely different commercial terms;
/// an earlier version of this system shared one timeout/budget/breaker across every connector, which
/// contradicted docs/03-suppliers-and-budget.md's own description of the budget as "per-session,
/// per-supplier." See docs/specs/tasks/07-look-to-book-budget-and-circuit-breaker.md.
/// <para>
/// <see cref="BudgetCeiling"/>/<see cref="BudgetWindow"/> and <see cref="BreakerFailureThreshold"/>/
/// <see cref="BreakerCooldown"/> are each an all-or-nothing pair: leave both null on a connector to run
/// it with no budget (or no breaker) at all, which is what task 06's own evals do — task 06 predates
/// the budget/breaker guardrails entirely.
/// </para>
/// </summary>
public sealed record SupplierPolicy(
    TimeSpan Timeout,
    int? BudgetCeiling = null,
    TimeSpan? BudgetWindow = null,
    int? BreakerFailureThreshold = null,
    TimeSpan? BreakerCooldown = null);
