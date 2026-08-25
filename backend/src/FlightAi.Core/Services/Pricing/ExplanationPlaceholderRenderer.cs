using System.Text.RegularExpressions;
using FlightAi.Core.Models.Pricing;

namespace FlightAi.Core.Services.Pricing;

/// <summary>
/// The only code allowed to turn a price-reference token into a digit. See docs/02-price-integrity.md.
/// Resolves every token via <see cref="PriceReferenceStore"/>, and rejects raw text where the model
/// wrote a number itself — as a digit or spelled out — instead of referencing a token. The digit/word
/// scan runs on the model's raw input, before any resolution, since resolution itself legitimately
/// introduces digits into the final text.
/// </summary>
public sealed partial class ExplanationPlaceholderRenderer(PriceReferenceStore store)
{
    private static readonly Regex TokenPattern = TokenRegex();
    private static readonly Regex DigitPattern = DigitRegex();

    // Magnitude words only -- deliberately excludes one-to-twenty spelled out ("one", "two", "um",
    // "dois", ...), which are common pronouns/adjectives in both languages and would produce an
    // unacceptable false-positive rate. See task 02's Locked decisions for the disclosed gap this
    // leaves: a model writing "five stops" instead of a STOPS token is not caught.
    private static readonly Regex MagnitudeWordPattern = MagnitudeWordsRegex();

    public RenderResult Render(string rawText)
    {
        var masked = MaskTokens(rawText);
        var violations = new List<string>();

        CollectMatches(DigitPattern, rawText, masked, "raw digit outside a token", violations);
        CollectMatches(MagnitudeWordPattern, rawText, masked, "spelled-out number outside a token", violations);

        if (violations.Count > 0)
            return new RenderResult(Success: false, Text: rawText, UnresolvedTokens: [], Violations: violations);

        var unresolved = new List<string>();
        var rendered = TokenPattern.Replace(rawText, match =>
        {
            if (store.TryResolve(match.Value, out var value))
                return value;
            unresolved.Add(match.Value);
            return match.Value; // left visibly unresolved, never silently dropped
        });

        return new RenderResult(Success: unresolved.Count == 0, Text: rendered, UnresolvedTokens: unresolved, Violations: []);
    }

    /// <summary>Replaces every token span with spaces of the same length, so later scans can treat
    /// token contents (which may legitimately contain digits, e.g. an offer ID like OFF8812) as
    /// invisible while preserving character offsets for diagnostic snippets.</summary>
    private static string MaskTokens(string text) => TokenPattern.Replace(text, m => new string(' ', m.Length));

    private static void CollectMatches(Regex pattern, string rawText, string masked, string label, List<string> violations)
    {
        foreach (Match match in pattern.Matches(masked))
        {
            violations.Add($"{label}: \"{Snippet(rawText, match.Index, match.Length)}\"");
        }
    }

    private static string Snippet(string text, int index, int length)
    {
        var start = Math.Max(0, index - 12);
        var end = Math.Min(text.Length, index + length + 12);
        return text[start..end];
    }

    [GeneratedRegex(@"\{\{[A-Za-z0-9_-]+\}\}", RegexOptions.Compiled)]
    private static partial Regex TokenRegex();

    [GeneratedRegex(@"\d+", RegexOptions.Compiled)]
    private static partial Regex DigitRegex();

    [GeneratedRegex(
        @"\b(hundred|thousand|million|billion|cem|cento|duzentos|trezentos|quatrocentos|quinhentos|seiscentos|setecentos|oitocentos|novecentos|mil|milh(?:a|õ)o|milh(?:a|õ)es|bilh(?:a|õ)o|bilh(?:a|õ)es)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex MagnitudeWordsRegex();
}
