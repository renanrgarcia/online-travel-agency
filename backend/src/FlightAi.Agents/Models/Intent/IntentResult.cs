using FlightAi.Core.Models.Offers;

namespace FlightAi.Agents.Models.Intent;

/// <summary>
/// The outcome of parsing a traveller's query into a <see cref="SearchRequest"/>. Failures are
/// returned here rather than thrown — both a malformed model response (invalid JSON) and a
/// semantically invalid one (missing destination, a past date) surface the same way, consistent with
/// the "failures are returned, not thrown" convention already used for suppliers (task 04).
/// </summary>
public sealed record IntentResult(bool Success, SearchRequest? Request, string? FailureReason)
{
    public static IntentResult Ok(SearchRequest request) => new(true, request, FailureReason: null);

    public static IntentResult Failed(string reason) => new(false, Request: null, reason);
}
