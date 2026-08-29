namespace FlightAi.Core.Models.Suppliers;

/// <summary>
/// One connector's operating parameters: timeout, look-to-book budget, and circuit-breaker
/// thresholds — all required. Real suppliers carry genuinely different commercial terms; an earlier
/// version of this system shared one timeout/budget/breaker across every connector, which
/// contradicted docs/reference/03-suppliers-and-budget.md's own description of the budget as "per-session,
/// per-supplier." A second version made budget and breaker optional per connector, which reopened the
/// same risk in a different shape: a connector with no budget configured ran unmetered, silently,
/// which is exactly the "contractual and financial incident" docs/reference/03-suppliers-and-budget.md warns
/// about. A policy that can be silently absent isn't a policy. See
/// docs/features/01-backend/tasks/07-look-to-book-budget-and-circuit-breaker.md.
/// </summary>
public sealed record SupplierPolicy(
    TimeSpan Timeout,
    int BudgetCeiling,
    TimeSpan BudgetWindow,
    int BreakerFailureThreshold,
    TimeSpan BreakerCooldown)
{
    /// <summary>
    /// A policy with a real timeout but no practical budget or breaker limit. For a caller that
    /// genuinely doesn't want either guardrail — this is the only sanctioned way to say so: stated
    /// explicitly, by name, at the call site, rather than left implicit by omitting a field. There is
    /// no path to "no limit" that doesn't go through here.
    /// </summary>
    public static SupplierPolicy WithNoLimits(TimeSpan timeout) => new(
        timeout,
        BudgetCeiling: int.MaxValue, BudgetWindow: TimeSpan.FromDays(365),
        BreakerFailureThreshold: int.MaxValue, BreakerCooldown: TimeSpan.FromDays(365));
}
