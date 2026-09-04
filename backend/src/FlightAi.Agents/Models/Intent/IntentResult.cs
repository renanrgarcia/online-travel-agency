using FlightAi.Core.Models.Offers;

namespace FlightAi.Agents.Models.Intent;

/// <summary>
/// The outcome of parsing a traveller's query into a <see cref="SearchRequest"/>. Failures are
/// returned here rather than thrown — both a malformed model response (invalid JSON) and a
/// semantically invalid one (missing destination, a past date) surface the same way, consistent with
/// the "failures are returned, not thrown" convention already used for suppliers (task 04).
/// </summary>
public sealed record IntentResult(
    bool Success,
    SearchRequest? Request,
    string? FailureReason,
    string? RawModelResponse = null,
    string? Code = null)
{
    public static IntentResult Ok(SearchRequest request) => new(true, request, FailureReason: null);

    /// <param name="code">A stable, machine-readable identifier for this specific failure -- e.g.
    /// "missing-departure-date" -- so a caller (the SSE error event, then the frontend) can show a
    /// friendly, localized message instead of this string, which is diagnostic text, not
    /// traveller-facing copy. Null for failures that don't yet have a dedicated frontend treatment.</param>
    public static IntentResult Failed(string reason, string? rawModelResponse = null, string? code = null) =>
        new(false, Request: null, reason, rawModelResponse, code);
}
