using System.Globalization;
using FlightAi.Core.Ranking;

namespace FlightAi.Core.Pricing;

public sealed record AgentVisibleOffer(
    int Rank,
    string OfferId,
    string Carrier,
    string PriceToken,
    string DurationToken,
    string StopsToken,
    string RefundableToken);

/// <summary>
/// The ground truth the render layer trusts — never the model's own typed-out text. A language model
/// must never be the thing that authors a number the traveller sees; that has to be enforced
/// structurally, not by asking the model nicely in a prompt. The explanation agent is handed opaque
/// tokens like <c>PRICE_OFF8812</c>, never the number itself.
/// It writes prose that references the tokens; only this store is allowed to resolve a token into a
/// digit, and it always resolves from the ranked offers, never from anything the model wrote.
///
/// Note what is deliberately absent: there is no <c>MARGIN_</c> token. Your commercial margin has no
/// resolvable token at all, so there is no path — intentional or hallucinated — for it to reach the
/// traveller-facing explanation.
/// </summary>
public sealed class PriceReferenceStore
{
    private readonly IReadOnlyList<ScoredOffer> _rankedOffers;
    private readonly Dictionary<string, ScoredOffer> _byId;

    public PriceReferenceStore(IReadOnlyList<ScoredOffer> rankedOffers)
    {
        _rankedOffers = rankedOffers;
        _byId = rankedOffers.ToDictionary(o => o.Offer.OfferId);
    }

    /// <summary>The token vocabulary for each offer — this, not the offer itself, is what the explanation agent's prompt is built from.</summary>
    public IReadOnlyList<AgentVisibleOffer> BuildAgentContext() =>
        _rankedOffers.Select((scored, index) =>
        {
            var id = scored.Offer.OfferId;
            return new AgentVisibleOffer(
                Rank: index + 1,
                OfferId: id,
                Carrier: scored.Offer.Segments[0].Carrier,
                PriceToken: $"PRICE_{id}",
                DurationToken: $"DURATION_{id}",
                StopsToken: $"STOPS_{id}",
                RefundableToken: $"REFUNDABLE_{id}");
        }).ToList();

    /// <summary>Comparison tokens so the agent can phrase "€40 more, three hours shorter" without ever doing the arithmetic itself.</summary>
    public IReadOnlyList<string> BuildComparisonTokens()
    {
        var tokens = new List<string>();
        foreach (var a in _rankedOffers)
            foreach (var b in _rankedOffers)
            {
                if (a.Offer.OfferId == b.Offer.OfferId) continue;
                tokens.Add($"PRICE_DELTA_{a.Offer.OfferId}_vs_{b.Offer.OfferId}");
                tokens.Add($"DURATION_DELTA_{a.Offer.OfferId}_vs_{b.Offer.OfferId}");
            }
        return tokens;
    }

    /// <summary>The only place in the system allowed to turn a token into a digit a traveller can see.</summary>
    public bool TryResolve(string token, out string formatted)
    {
        formatted = "";

        if (TryMatchSingle(token, "PRICE_", out var id) && _byId.TryGetValue(id, out var forPrice))
        {
            formatted = $"{FormatMoney(forPrice.Offer.TotalPrice)} {forPrice.Offer.Currency}";
            return true;
        }
        if (TryMatchSingle(token, "DURATION_", out id) && _byId.TryGetValue(id, out var forDuration))
        {
            formatted = FormatDuration(forDuration.Offer.TotalDuration);
            return true;
        }
        if (TryMatchSingle(token, "STOPS_", out id) && _byId.TryGetValue(id, out var forStops))
        {
            formatted = forStops.Offer.StopCount switch { 0 => "nonstop", 1 => "1 stop", var n => $"{n} stops" };
            return true;
        }
        if (TryMatchSingle(token, "REFUNDABLE_", out id) && _byId.TryGetValue(id, out var forRules))
        {
            formatted = forRules.Offer.FareRules.Refundable ? "refundable" : "non-refundable";
            return true;
        }
        if (TryMatchPair(token, "PRICE_DELTA_", out var a, out var b) &&
            _byId.TryGetValue(a, out var priceA) && _byId.TryGetValue(b, out var priceB))
        {
            var delta = priceA.Offer.TotalPrice - priceB.Offer.TotalPrice;
            formatted = $"{FormatMoney(Math.Abs(delta))} {priceA.Offer.Currency} {(delta >= 0 ? "more" : "less")}";
            return true;
        }
        if (TryMatchPair(token, "DURATION_DELTA_", out a, out b) &&
            _byId.TryGetValue(a, out var durationA) && _byId.TryGetValue(b, out var durationB))
        {
            var delta = durationA.Offer.TotalDuration - durationB.Offer.TotalDuration;
            formatted = $"{FormatDuration(Abs(delta))} {(delta >= TimeSpan.Zero ? "longer" : "shorter")}";
            return true;
        }

        return false; // Unknown token — including any attempt to reference MARGIN_. Never guess.
    }

    private static TimeSpan Abs(TimeSpan t) => t < TimeSpan.Zero ? -t : t;
    private static string FormatDuration(TimeSpan d) => $"{(int)d.TotalHours}h {d.Minutes}m";

    /// <summary>
    /// Explicit invariant-culture formatting — not a stylistic choice. Left to ambient
    /// CultureInfo.CurrentCulture, this renders "500.00" as "500,00" on a machine whose OS region uses
    /// a comma decimal separator, silently, with no error. A price string is exactly the kind of value
    /// this system cannot afford to get wrong depending on which server happened to run the process.
    /// Real per-traveller locale formatting is a presentation-layer decision, not this store's job.
    /// </summary>
    private static string FormatMoney(decimal amount) => amount.ToString("0.00", CultureInfo.InvariantCulture);

    private static bool TryMatchSingle(string token, string prefix, out string id)
    {
        id = "";
        if (!token.StartsWith(prefix, StringComparison.Ordinal)) return false;
        id = token[prefix.Length..];
        return id.Length > 0;
    }

    private static bool TryMatchPair(string token, string prefix, out string a, out string b)
    {
        a = ""; b = "";
        if (!token.StartsWith(prefix, StringComparison.Ordinal)) return false;
        var parts = token[prefix.Length..].Split("_vs_");
        if (parts.Length != 2) return false;
        (a, b) = (parts[0], parts[1]);
        return true;
    }
}
