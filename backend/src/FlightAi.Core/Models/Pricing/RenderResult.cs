namespace FlightAi.Core.Models.Pricing;

/// <summary>
/// The result of a render attempt. <see cref="Violations"/> non-empty means the model's raw text
/// bypassed the token mechanism (a digit or spelled-out number outside any token) — <see cref="Text"/>
/// is the original, unrendered input in that case, and must never be shown to a user.
/// </summary>
public sealed record RenderResult(
    bool Success,
    string Text,
    IReadOnlyList<string> UnresolvedTokens,
    IReadOnlyList<string> Violations);
