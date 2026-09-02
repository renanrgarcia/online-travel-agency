using System.Globalization;

namespace FlightAi.Core.Services.Pricing;

/// <summary>A fact about one offer relative to others, not just a value about the offer itself
/// (task 18) — the same "never author it yourself" guarantee task 01 gives numbers, extended to
/// comparisons: "cheapest", "fastest", "fewest stops", and "the only refundable option" are each
/// resolved token text, never a word the explanation agent is trusted to choose on its own.</summary>
public enum Superlative
{
    Cheapest,
    Fastest,
    FewestStops,
    OnlyRefundable,
}

/// <summary>
/// Hands out opaque tokens for values a traveller may see (price, duration, stops, refund status, and
/// — task 18 — comparisons between offers) instead of the values themselves. See
/// docs/reference/02-price-integrity.md — this is one side of the price-integrity boundary;
/// <see cref="ExplanationPlaceholderRenderer"/> (task 02) is the other. Only that renderer should ever
/// call <see cref="TryResolve"/> — nothing that generates free text (an AI agent) should hold a
/// reference to this store.
/// <para>
/// Resolved text follows <paramref name="language"/> (task 18) -- a live gap this task closes rather
/// than defers: task 11's Portuguese evals only ever checked that rendering succeeded, never that the
/// rendered words actually were Portuguese, so this store had been silently English-only for every
/// phrase (nonstop, refundable, "more"/"less") since task 01. Only <em>words</em> are localised; number
/// and date formats stay invariant-culture, per task 01's own locked decision.
/// </para>
/// </summary>
public sealed class PriceReferenceStore(string language = "en")
{
    private readonly Dictionary<string, string> _resolved = new(StringComparer.Ordinal);

    public string RegisterPrice(string offerId, decimal amount, string currency)
    {
        var token = Token("PRICE", offerId);
        _resolved[token] = FormatCurrency(amount, currency);
        return token;
    }

    public string RegisterDuration(string offerId, TimeSpan duration)
    {
        var token = Token("DURATION", offerId);
        _resolved[token] = FormatDuration(duration);
        return token;
    }

    public string RegisterStops(string offerId, int stops)
    {
        var token = Token("STOPS", offerId);
        _resolved[token] = stops switch
        {
            0 => Localize("nonstop", "sem escalas"),
            1 => Localize("1 stop", "1 escala"),
            _ => Localize($"{stops} stops", $"{stops} escalas"),
        };
        return token;
    }

    public string RegisterRefundable(string offerId, bool refundable)
    {
        var token = Token("REFUNDABLE", offerId);
        _resolved[token] = refundable
            ? Localize("refundable", "reembolsável")
            : Localize("non-refundable", "não reembolsável");
        return token;
    }

    /// <summary>
    /// Registers a price comparison between two offers, e.g. "$42.00 more" or "$42.00 less".
    /// <paramref name="delta"/> is B's price minus A's.
    /// </summary>
    public string RegisterPriceDelta(string offerIdA, string offerIdB, decimal delta, string currency)
    {
        var token = $"{{{{PRICE_DELTA_{offerIdA}_vs_{offerIdB}}}}}";
        _resolved[token] = delta == 0
            ? Localize("the same price", "o mesmo preço")
            : $"{FormatCurrency(Math.Abs(delta), currency)} {MoreOrLess(delta)}";
        return token;
    }

    /// <summary>
    /// The duration equivalent of <see cref="RegisterPriceDelta"/> (task 18 E2): a magnitude plus a
    /// direction, e.g. "3h shorter" / "3h longer". <paramref name="delta"/> is B's duration minus A's.
    /// </summary>
    public string RegisterDurationDelta(string offerIdA, string offerIdB, TimeSpan delta)
    {
        var token = $"{{{{DURATION_DELTA_{offerIdA}_vs_{offerIdB}}}}}";
        _resolved[token] = delta == TimeSpan.Zero
            ? Localize("the same duration", "a mesma duração")
            : $"{FormatDuration(delta.Duration())} {ShorterOrLonger(delta)}";
        return token;
    }

    /// <summary>
    /// A superlative fact about one offer, e.g. "the cheapest option" (task 18) -- the caller decides
    /// *whether* an offer genuinely, uniquely holds a superlative (this store has no opinion on that);
    /// registering one here only ever produces resolved text, never the decision itself.
    /// </summary>
    public string RegisterSuperlative(string offerId, Superlative superlative)
    {
        var token = $"{{{{SUPERLATIVE_{superlative.ToString().ToUpperInvariant()}_{offerId}}}}}";
        _resolved[token] = superlative switch
        {
            Superlative.Cheapest => Localize("the cheapest option", "a opção mais barata"),
            Superlative.Fastest => Localize("the fastest option", "a opção mais rápida"),
            Superlative.FewestStops => Localize("the option with the fewest stops", "a opção com menos escalas"),
            Superlative.OnlyRefundable => Localize("the only refundable option", "a única opção reembolsável"),
            _ => throw new ArgumentOutOfRangeException(nameof(superlative)),
        };
        return token;
    }

    /// <summary>
    /// Resolves a token to its display text. Returns false for anything the store never issued —
    /// including a hallucinated MARGIN_ reference, which can never resolve because no such token
    /// is ever registered. Callers must not silently drop an unresolved token; see
    /// docs/reference/02-price-integrity.md for why.
    /// </summary>
    public bool TryResolve(string token, out string value) => _resolved.TryGetValue(token, out value!);

    private string Localize(string english, string portuguese) => language == "pt-BR" ? portuguese : english;

    private string MoreOrLess(decimal delta) => delta switch
    {
        > 0 => Localize("more", "a mais"),
        _ => Localize("less", "a menos"),
    };

    private string ShorterOrLonger(TimeSpan delta) => delta switch
    {
        { Ticks: > 0 } => Localize("longer", "mais longo"),
        _ => Localize("shorter", "mais curto"),
    };

    private static string Token(string kind, string offerId) => $"{{{{{kind}_{offerId}}}}}";

    private static string FormatCurrency(decimal amount, string currency) =>
        currency switch
        {
            "USD" => amount.ToString("$0.00", CultureInfo.InvariantCulture),
            "BRL" => amount.ToString("R$0.00", CultureInfo.InvariantCulture),
            "EUR" => amount.ToString("€0.00", CultureInfo.InvariantCulture),
            _ => $"{amount.ToString("0.00", CultureInfo.InvariantCulture)} {currency}",
        };

    private static string FormatDuration(TimeSpan duration)
    {
        var hours = (int)duration.TotalHours;
        var minutes = duration.Minutes;
        return minutes == 0 ? $"{hours}h" : $"{hours}h {minutes}m";
    }
}
