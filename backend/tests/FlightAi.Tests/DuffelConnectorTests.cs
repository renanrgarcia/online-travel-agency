using System.Net;
using FlightAi.Core.Models.Offers;
using FlightAi.Core.Models.Suppliers;
using FlightAi.Core.Services.Suppliers;
using Xunit;

namespace FlightAi.Tests;

/// <summary>
/// One test per eval in docs/features/01-backend/tasks/25-duffel-supplier-connector.md that doesn't
/// need a live Duffel test-mode token -- the response-mapping and error-handling logic, exercised
/// against a fake <see cref="HttpMessageHandler"/> with hand-built (but schema-accurate, per Duffel's
/// own published docs) response bodies. E1 and E5 specifically require a real test-mode token and are
/// verified separately, live, not here -- the same split task 17 used for the model layer.
/// </summary>
public class DuffelConnectorTests
{
    private static readonly SearchRequest Request = new("GRU", "LIS", new DateOnly(2027, 3, 12), 1, "en");

    private static DuffelConnector NewConnector(FakeHttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://api.duffel.com/") });

    [Fact]
    public async Task SuccessfulResponse_MapsToOfferCleanly()
    {
        var handler = FakeHttpMessageHandler.RespondingWith(HttpStatusCode.OK, """
            {
              "data": {
                "offers": [
                  {
                    "id": "off_00009htYpSCXrwaB9DnUm0",
                    "total_amount": "445.50",
                    "total_currency": "USD",
                    "expires_at": "2027-03-12T10:42:14.545Z",
                    "conditions": { "refund_before_departure": { "allowed": true } },
                    "slices": [
                      {
                        "duration": "PT7H30M",
                        "segments": [
                          { "id": "seg_1" },
                          { "id": "seg_2" }
                        ]
                      }
                    ]
                  }
                ]
              }
            }
            """);

        var result = await NewConnector(handler).SearchAsync(Request, CancellationToken.None);

        Assert.Equal(SupplierOutcome.Success, result.Outcome);
        var offer = Assert.Single(result.Offers);
        Assert.Equal("Duffel-off_00009htYpSCXrwaB9DnUm0", offer.OfferId);
        Assert.Equal(445.50m, offer.Price);
        Assert.Equal("USD", offer.Currency);
        Assert.Equal(TimeSpan.FromMinutes(450), offer.Duration);
        Assert.Equal(1, offer.Stops); // two segments -> one stop
        Assert.True(offer.Refundable);
        Assert.Equal(0m, offer.Margin);
        Assert.Equal(new DateTimeOffset(2027, 3, 12, 10, 42, 14, 545, TimeSpan.Zero), offer.ExpiresAt);
    }

    [Fact] // E2 (against a hand-built error shape rather than a live unresolvable-IATA-code call)
    public async Task NonSuccessStatusCode_ReturnsFailureNotThrown()
    {
        var handler = FakeHttpMessageHandler.RespondingWith(HttpStatusCode.UnprocessableEntity, """
            {"errors":[{"title":"Invalid airport code","message":"Could not resolve origin"}]}
            """);

        var result = await NewConnector(handler).SearchAsync(Request, CancellationToken.None);

        Assert.Equal(SupplierOutcome.Failure, result.Outcome);
        Assert.Empty(result.Offers);
        Assert.Contains("422", result.FailureReason);
    }

    [Fact]
    public async Task MalformedJsonResponse_ReturnsFailureNotThrown()
    {
        var handler = FakeHttpMessageHandler.RespondingWith(HttpStatusCode.OK, "this is not json");

        var result = await NewConnector(handler).SearchAsync(Request, CancellationToken.None);

        Assert.Equal(SupplierOutcome.Failure, result.Outcome);
        Assert.NotNull(result.FailureReason);
    }

    [Fact] // E3 (against an already-cancelled token rather than a live slow response)
    public async Task Cancellation_ReturnsCancelledOutcomeNotAnException()
    {
        var handler = FakeHttpMessageHandler.ThrowingOperationCanceled();

        var result = await NewConnector(handler).SearchAsync(Request, CancellationToken.None);

        Assert.Equal(SupplierOutcome.Cancelled, result.Outcome);
    }

    [Fact]
    public async Task EmptyOffersList_IsSuccessNotFailure()
    {
        var handler = FakeHttpMessageHandler.RespondingWith(HttpStatusCode.OK, """{"data": {"offers": []}}""");

        var result = await NewConnector(handler).SearchAsync(Request, CancellationToken.None);

        Assert.Equal(SupplierOutcome.Success, result.Outcome);
        Assert.Empty(result.Offers);
    }

    [Fact]
    public async Task OneUnmappableOfferAmongValidOnes_SkipsOnlyTheBadOne()
    {
        var handler = FakeHttpMessageHandler.RespondingWith(HttpStatusCode.OK, """
            {
              "data": {
                "offers": [
                  {
                    "id": "off_bad",
                    "total_amount": "100.00",
                    "total_currency": "USD",
                    "expires_at": "2027-03-12T10:42:14.545Z",
                    "slices": [ { "duration": null, "segments": [ { "id": "seg_1" } ] } ]
                  },
                  {
                    "id": "off_good",
                    "total_amount": "200.00",
                    "total_currency": "USD",
                    "expires_at": "2027-03-12T10:42:14.545Z",
                    "slices": [ { "duration": "PT2H0M", "segments": [ { "id": "seg_1" } ] } ]
                  }
                ]
              }
            }
            """);

        var result = await NewConnector(handler).SearchAsync(Request, CancellationToken.None);

        Assert.Equal(SupplierOutcome.Success, result.Outcome);
        var offer = Assert.Single(result.Offers);
        Assert.Equal("Duffel-off_good", offer.OfferId);
    }

    [Fact]
    public async Task NoDepartureDate_ReturnsFailureNotThrown()
    {
        var requestWithNoDate = Request with { DepartureDate = null };
        var handler = FakeHttpMessageHandler.RespondingWith(HttpStatusCode.OK, """{"data": {"offers": []}}""");

        var result = await NewConnector(handler).SearchAsync(requestWithNoDate, CancellationToken.None);

        Assert.Equal(SupplierOutcome.Failure, result.Outcome);
        Assert.False(handler.WasCalled);
    }
}

/// <summary>Test-only stand-in for the real Duffel HTTP endpoint -- returns a canned response or throws,
/// never makes a real network call.</summary>
public sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpResponseMessage> _respond;
    public bool WasCalled { get; private set; }

    private FakeHttpMessageHandler(Func<HttpResponseMessage> respond) => _respond = respond;

    public static FakeHttpMessageHandler RespondingWith(HttpStatusCode status, string body) =>
        new(() => new HttpResponseMessage(status) { Content = new StringContent(body) });

    public static FakeHttpMessageHandler ThrowingOperationCanceled() =>
        new(() => throw new TaskCanceledException());

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        WasCalled = true;
        return Task.FromResult(_respond());
    }
}
