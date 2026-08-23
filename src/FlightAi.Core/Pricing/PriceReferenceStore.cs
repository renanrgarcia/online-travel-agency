using System.Globalization;

namespace FlightAi.Core.Pricing;

/// <summary>
/// Hands out opaque tokens for values a traveller may see (price, duration, stops, refund status)
/// instead of the values themselves. See docs/02-price-integrity.md — this is one side of the
/// price-integrity boundary; <see cref="ExplanationPlaceholderRenderer"/> (task 02) is the other.
/// Only that renderer should ever call <see cref="TryResolve"/> — nothing that generates free text
/// (an AI agent) should hold a reference to this store.
/// </summary>
public sealed class PriceReferenceStore
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
            0 => "nonstop",
            1 => "1 stop",
            _ => $"{stops} stops",
        };
        return token;
    }

    public string RegisterRefundable(string offerId, bool refundable)
    {
        var token = Token("REFUNDABLE", offerId);
        _resolved[token] = refundable ? "refundable" : "non-refundable";
        return token;
    }

    /// <summary>
    /// Registers a price comparison between two offers, e.g. "$42.00 more" or "$42.00 less".
    /// <paramref name="delta"/> is B's price minus A's.
    /// </summary>
    public string RegisterPriceDelta(string offerIdA, string offerIdB, decimal delta, string currency)
    {
        var token = $"{{{{PRICE_DELTA_{offerIdA}_vs_{offerIdB}}}}}";
        var direction = delta switch
        {
            > 0 => "more",
            < 0 => "less",
            _ => "the same",
        };
        _resolved[token] = delta == 0
            ? "the same price"
            : $"{FormatCurrency(Math.Abs(delta), currency)} {direction}";
        return token;
    }

    /// <summary>
    /// Resolves a token to its display text. Returns false for anything the store never issued —
    /// including a hallucinated MARGIN_ reference, which can never resolve because no such token
    /// is ever registered. Callers must not silently drop an unresolved token; see
    /// docs/02-price-integrity.md for why.
    /// </summary>
    public bool TryResolve(string token, out string value) => _resolved.TryGetValue(token, out value!);

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
