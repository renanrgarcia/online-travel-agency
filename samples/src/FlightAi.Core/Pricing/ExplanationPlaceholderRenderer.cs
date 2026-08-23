using System.Text.RegularExpressions;

namespace FlightAi.Core.Pricing;

/// <summary>
/// The render half of the price-integrity boundary. The explanation agent's raw output may contain
/// <c>{{TOKEN}}</c> placeholders — this is the only code allowed to turn one into a digit the
/// traveller sees, and it always resolves through <see cref="PriceReferenceStore"/>. A token the
/// store does not recognise is never silently dropped: it is left visibly unresolved so a bad or
/// hallucinated reference fails loudly instead of quietly leaking whatever the model typed nearby.
///
/// This also enforces the other half of the same rule. A well-behaved model only ever puts numbers
/// inside tokens — but "the model was told not to write raw digits" is a prompt-level instruction, not
/// a guarantee: a model can ignore it. So this renderer checks the model's
/// raw text for any digit sitting <em>outside</em> a token span at all. A model that types "$999"
/// directly into its prose, instead of referencing a token, fails this check even though no token was
/// involved — that is precisely the failure mode structural enforcement exists to catch.
/// </summary>
public static partial class ExplanationPlaceholderRenderer
{
    [GeneratedRegex(@"\{\{([A-Za-z0-9_-]+)\}\}")]
    private static partial Regex TokenPattern();

    [GeneratedRegex(@"\d")]
    private static partial Regex AnyDigit();

    public sealed record RenderResult(
        string Text,
        IReadOnlyList<string> UnresolvedTokens,
        bool HasUnmatchedBraces,
        bool HasStrayDigitsOutsideTokens)
    {
        /// <summary>
        /// False if a recognised token failed to resolve, OR a literal "{{" survives rendering (a
        /// malformed token that never matched the pattern in the first place), OR the model's raw text
        /// contained a digit anywhere outside a token span. Any one of these means a number could have
        /// reached the traveller without passing through the ground-truth store.
        /// </summary>
        public bool IsClean => UnresolvedTokens.Count == 0 && !HasUnmatchedBraces && !HasStrayDigitsOutsideTokens;
    }

    public static RenderResult Render(string modelText, PriceReferenceStore store)
    {
        var unresolved = new List<string>();

        var rendered = TokenPattern().Replace(modelText, match =>
        {
            var token = match.Groups[1].Value;
            if (store.TryResolve(token, out var value)) return value;

            unresolved.Add(token);
            return $"[unresolved:{token}]";
        });

        var strayDigits = AnyDigit().IsMatch(TokenPattern().Replace(modelText, ""));

        return new RenderResult(
            rendered,
            unresolved,
            HasUnmatchedBraces: rendered.Contains("{{", StringComparison.Ordinal),
            HasStrayDigitsOutsideTokens: strayDigits);
    }
}
