namespace FlightAi.Agents.Models.Explanation;

/// <summary>
/// One offer's price-integrity tokens (task 01) — never its real values. This is the only shape
/// <c>ExplanationAgent</c> ever sees an offer in; there is no path from here back to a real price,
/// duration, stop count, or refund status. See docs/features/01-backend/tasks/11-explanation-agent.md.
/// </summary>
public sealed record TokenizedOffer(
    string OfferId,
    string PriceToken,
    string DurationToken,
    string StopsToken,
    string RefundableToken);
