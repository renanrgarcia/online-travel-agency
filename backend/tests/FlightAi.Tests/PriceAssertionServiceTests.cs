using FlightAi.Core.Services.Pricing;
using Xunit;

namespace FlightAi.Tests;

/// <summary>
/// One test per relevant eval in docs/features/01-backend/tasks/21-server-authoritative-offer-prices.md
/// against <see cref="PriceAssertionService"/> directly -- the cryptographic correctness the two-host
/// end-to-end evals (E1, E6) depend on, isolated from HTTP and Azurite so it stays fast and deterministic.
/// </summary>
public class PriceAssertionServiceTests
{
    private sealed class FakeClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }

    private static FakeClock NewClock() => new(new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero));

    [Fact] // baseline -- an assertion issued by the service verifies against itself
    public void IssuedAssertion_VerifiesSuccessfully()
    {
        var service = new PriceAssertionService("signing-key", TimeSpan.FromMinutes(5), NewClock());

        var assertion = service.Issue("LCC-002", 590.00m, "USD");

        Assert.True(service.TryVerify(assertion, out _));
    }

    [Fact] // E2/E3 -- a tampered amount is rejected, the whole point of signing at all
    public void TamperedAmount_IsRejectedAsInvalidSignature()
    {
        var service = new PriceAssertionService("signing-key", TimeSpan.FromMinutes(5), NewClock());
        var assertion = service.Issue("LCC-002", 590.00m, "USD");

        var tampered = assertion with { Amount = 1.00m };

        Assert.False(service.TryVerify(tampered, out var failure));
        Assert.Equal(PriceAssertionFailure.InvalidSignature, failure);
    }

    [Fact] // E3 -- any field tampered, not just amount, invalidates the signature
    public void TamperedOfferId_IsRejectedAsInvalidSignature()
    {
        var service = new PriceAssertionService("signing-key", TimeSpan.FromMinutes(5), NewClock());
        var assertion = service.Issue("LCC-002", 590.00m, "USD");

        var tampered = assertion with { OfferId = "LCC-001" };

        Assert.False(service.TryVerify(tampered, out var failure));
        Assert.Equal(PriceAssertionFailure.InvalidSignature, failure);
    }

    [Fact] // E4 -- signed by the wrong key entirely
    public void AssertionSignedByADifferentKey_IsRejectedAsInvalidSignature()
    {
        var issuer = new PriceAssertionService("key-one", TimeSpan.FromMinutes(5), NewClock());
        var verifier = new PriceAssertionService("key-two", TimeSpan.FromMinutes(5), NewClock());

        var assertion = issuer.Issue("LCC-002", 590.00m, "USD");

        Assert.False(verifier.TryVerify(assertion, out var failure));
        Assert.Equal(PriceAssertionFailure.InvalidSignature, failure);
    }

    [Fact] // garbage instead of a real signature must not throw
    public void MalformedSignature_IsRejectedRatherThanThrowing()
    {
        var service = new PriceAssertionService("signing-key", TimeSpan.FromMinutes(5), NewClock());
        var assertion = service.Issue("LCC-002", 590.00m, "USD");

        var malformed = assertion with { Signature = "not valid base64!!" };

        Assert.False(service.TryVerify(malformed, out var failure));
        Assert.Equal(PriceAssertionFailure.InvalidSignature, failure);
    }

    [Fact] // E5 -- expired is a distinct, defined reason, not folded into "invalid"
    public void ExpiredAssertion_IsRejectedAsExpiredNotInvalidSignature()
    {
        var clock = NewClock();
        var service = new PriceAssertionService("signing-key", TimeSpan.FromMinutes(5), clock);
        var assertion = service.Issue("LCC-002", 590.00m, "USD");

        clock.Advance(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));

        Assert.False(service.TryVerify(assertion, out var failure));
        Assert.Equal(PriceAssertionFailure.Expired, failure);
    }

    [Fact] // signature verification runs before the expiry check -- a tampered AND expired assertion
    // still reports the more serious failure, not whichever happens to be checked first by accident
    public void TamperedAndExpiredAssertion_ReportsInvalidSignatureNotExpired()
    {
        var clock = NewClock();
        var service = new PriceAssertionService("signing-key", TimeSpan.FromMinutes(5), clock);
        var assertion = service.Issue("LCC-002", 590.00m, "USD") with { Amount = 1.00m };

        clock.Advance(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));

        Assert.False(service.TryVerify(assertion, out var failure));
        Assert.Equal(PriceAssertionFailure.InvalidSignature, failure);
    }

    [Fact] // E8 -- freshness, not a replayable fixed token: two issues for the same offer differ
    public void TwoIssuesForTheSameOffer_ProduceDifferentAssertionsBothOfWhichVerify()
    {
        var clock = NewClock();
        var service = new PriceAssertionService("signing-key", TimeSpan.FromMinutes(5), clock);

        var first = service.Issue("LCC-002", 590.00m, "USD");
        clock.Advance(TimeSpan.FromSeconds(1));
        var second = service.Issue("LCC-002", 590.00m, "USD");

        Assert.NotEqual(first.ExpiresAt, second.ExpiresAt);
        Assert.NotEqual(first.Signature, second.Signature);
        Assert.True(service.TryVerify(first, out _));
        Assert.True(service.TryVerify(second, out _));
    }

    [Fact] // a signing key is mandatory, no silent fallback to something predictable
    public void MissingSigningKey_ThrowsAtConstruction()
    {
        Assert.Throws<ArgumentException>(() => new PriceAssertionService("", TimeSpan.FromMinutes(5)));
        Assert.Throws<ArgumentException>(() => new PriceAssertionService("   ", TimeSpan.FromMinutes(5)));
    }
}
