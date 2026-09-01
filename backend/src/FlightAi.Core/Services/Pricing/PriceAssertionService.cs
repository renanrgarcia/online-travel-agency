using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace FlightAi.Core.Services.Pricing;

/// <summary>
/// A signed, time-boxed assertion that <see cref="OfferId"/>'s authoritative price is
/// <see cref="Amount"/>/<see cref="Currency"/> as of <see cref="ExpiresAt"/>. Safe to hand to a
/// browser -- it carries nothing the client didn't already see in the same search response, and no
/// signing key. See docs/reference/02-price-integrity.md, task 21.
/// </summary>
public sealed record PriceAssertion(string OfferId, decimal Amount, string Currency, DateTimeOffset ExpiresAt, string Signature);

/// <summary>Why <see cref="PriceAssertionService.TryVerify"/> rejected an assertion, kept distinct from
/// a bare bool so a caller (task 21 E5) can tell "this was tampered with or forged" apart from "this
/// was genuine, but the traveller took too long to book."</summary>
public enum PriceAssertionFailure
{
    InvalidSignature,
    Expired,
}

/// <summary>
/// Issues and verifies <see cref="PriceAssertion"/>s with HMAC-SHA256, so a booking is authorized at
/// the price the search API actually quoted, never whatever a client's request body claims (task 21).
/// <para>
/// The API and the booking saga are separate hosts with no shared datastore. A signed assertion needs
/// neither: both hosts hold the same signing key in configuration (never source -- the same rule task
/// 17 applies to the model key), and verification is pure computation, no lookup, no I/O, nothing to
/// keep in sync but the one key.
/// </para>
/// </summary>
public sealed class PriceAssertionService
{
    private readonly byte[] _key;
    private readonly TimeSpan _validity;
    private readonly TimeProvider _timeProvider;

    public PriceAssertionService(string signingKey, TimeSpan validity, TimeProvider? timeProvider = null)
    {
        if (string.IsNullOrWhiteSpace(signingKey))
            throw new ArgumentException("A price assertion signing key is required.", nameof(signingKey));

        _key = Encoding.UTF8.GetBytes(signingKey);
        _validity = validity;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public PriceAssertion Issue(string offerId, decimal amount, string currency)
    {
        var expiresAt = _timeProvider.GetUtcNow() + _validity;
        return new PriceAssertion(offerId, amount, currency, expiresAt, Sign(offerId, amount, currency, expiresAt));
    }

    /// <summary>
    /// Verifies the signature first, and only then checks expiry -- an assertion whose signature
    /// doesn't match can't have its own <see cref="PriceAssertion.ExpiresAt"/> trusted either, since
    /// that field could be exactly what was tampered with.
    /// </summary>
    public bool TryVerify(PriceAssertion assertion, out PriceAssertionFailure failure)
    {
        var expected = ComputeSignature(assertion.OfferId, assertion.Amount, assertion.Currency, assertion.ExpiresAt);
        Span<byte> provided = stackalloc byte[expected.Length];
        var validSignature = Convert.TryFromBase64String(assertion.Signature, provided, out var bytesWritten)
            && bytesWritten == expected.Length
            && CryptographicOperations.FixedTimeEquals(provided, expected);

        if (!validSignature)
        {
            failure = PriceAssertionFailure.InvalidSignature;
            return false;
        }

        if (assertion.ExpiresAt < _timeProvider.GetUtcNow())
        {
            failure = PriceAssertionFailure.Expired;
            return false;
        }

        failure = default;
        return true;
    }

    private string Sign(string offerId, decimal amount, string currency, DateTimeOffset expiresAt) =>
        Convert.ToBase64String(ComputeSignature(offerId, amount, currency, expiresAt));

    // Constant-time comparison (CryptographicOperations.FixedTimeEquals in TryVerify) is what matters
    // here -- a plain byte-array equality check would let an attacker forge a signature one byte at a
    // time by timing how long each guess takes to be rejected.
    private byte[] ComputeSignature(string offerId, decimal amount, string currency, DateTimeOffset expiresAt)
    {
        var payload = $"{offerId}|{amount.ToString("F2", CultureInfo.InvariantCulture)}|{currency}|{expiresAt:O}";
        return HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(payload));
    }
}
