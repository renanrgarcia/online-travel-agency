namespace FlightAi.Agents.Models.Explanation;

/// <summary>
/// One offer's price-integrity tokens (task 01) — never its real values. This is the only shape
/// <c>ExplanationAgent</c> ever sees an offer in; there is no path from here back to a real price,
/// duration, stop count, or refund status. See docs/features/01-backend/tasks/11-explanation-agent.md.
/// <para>
/// <see cref="PriceDeltaToken"/> / <see cref="DurationDeltaToken"/> / <see cref="SuperlativeTokens"/>
/// (task 18) are comparisons, not values. The deltas are <see langword="null"/> for the top-ranked
/// offer (nothing to compare it against). <see cref="SuperlativeTokens"/> is a list, empty by default,
/// rather than a single nullable slot, since one offer can genuinely hold more than one superlative at
/// once (often the cheapest offer is also the fastest) — collapsing to one would silently drop a true
/// fact.
/// </para>
/// </summary>
public sealed record TokenizedOffer(
    string OfferId,
    string PriceToken,
    string DurationToken,
    string StopsToken,
    string RefundableToken,
    string? PriceDeltaToken = null,
    string? DurationDeltaToken = null,
    IReadOnlyList<string>? SuperlativeTokens = null)
{
    public IReadOnlyList<string> SuperlativeTokens { get; init; } = SuperlativeTokens ?? [];
}
