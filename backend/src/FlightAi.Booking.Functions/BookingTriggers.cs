using System.Net;
using System.Text.Json;
using FlightAi.Booking.Functions.Models;
using FlightAi.Core.Services.Pricing;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FlightAi.Booking.Functions;

/// <summary>
/// <c>POST /api/bookings</c> and <c>GET /api/bookings/{bookingId}</c>, matching
/// <c>docs/reference/07-booking-saga.md</c>'s contract exactly. JSON is read/written manually with our
/// own <see cref="JsonSerializerOptions"/> rather than via <c>WriteAsJsonAsync</c>'s <c>ObjectSerializer</c>
/// overload, both to guarantee camelCase field names on our own envelope (Durable Task's own
/// serialization of <see cref="BookingResult"/>, embedded as a string, is untouched) and to avoid
/// stacking a second <c>Content-Type</c> header the way <c>docs/reference/09-lessons-learned.md</c>
/// already documents going wrong once.
/// </summary>
public static class BookingTriggers
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Function(nameof(CreateBooking))]
    public static async Task<HttpResponseData> CreateBooking(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "bookings")] HttpRequestData request,
        [DurableClient] DurableTaskClient client,
        FunctionContext executionContext)
    {
        var logger = executionContext.GetLogger(nameof(CreateBooking));
        // Static Function methods can't take constructor-injected dependencies the way an instance
        // class could -- InstanceServices is the isolated worker's own per-invocation service
        // provider, scoped exactly like a real DI scope, for exactly this case.
        var priceAssertionService = executionContext.InstanceServices.GetRequiredService<PriceAssertionService>();
        var httpRequest = await JsonSerializer.DeserializeAsync<CreateBookingRequest>(request.Body, JsonOptions);
        if (httpRequest is null)
            return await WriteJson(request, HttpStatusCode.BadRequest, new { error = "Request body is required." });

        // The server's value always wins (task 21 locked decision): the request body's Amount/Currency
        // are advisory at most. Rejected here, before any orchestration is scheduled -- the same
        // "reject before the expensive/stateful work" principle task 20 applies to rate limiting.
        if (httpRequest.PriceAssertion is null)
        {
            return await WriteJson(request, HttpStatusCode.BadRequest,
                new { error = "A price assertion is required.", reason = "missing_price_assertion" });
        }

        if (!string.Equals(httpRequest.PriceAssertion.OfferId, httpRequest.OfferId, StringComparison.Ordinal))
        {
            return await WriteJson(request, HttpStatusCode.BadRequest,
                new { error = "The price assertion is for a different offer.", reason = "price_assertion_offer_mismatch" });
        }

        if (!priceAssertionService.TryVerify(httpRequest.PriceAssertion, out var failure))
        {
            var (error, reason) = failure switch
            {
                PriceAssertionFailure.Expired => ("The price assertion has expired.", "price_assertion_expired"),
                _ => ("The price assertion is invalid.", "price_assertion_invalid"),
            };
            return await WriteJson(request, HttpStatusCode.BadRequest, new { error, reason });
        }

        // Constructed from the verified assertion, never the request body -- BookingRequest carries
        // exactly what AuthorizePayment will actually charge.
        var bookingRequest = new BookingRequest(
            httpRequest.BookingId, httpRequest.OfferId, httpRequest.TravellerEmail,
            httpRequest.PriceAssertion.Amount, httpRequest.PriceAssertion.Currency, httpRequest.PaymentMethodToken);

        var existing = await client.GetInstanceAsync(bookingRequest.BookingId, getInputsAndOutputs: false);
        if (existing is null)
        {
            await client.ScheduleNewOrchestrationInstanceAsync(
                nameof(BookingOrchestrator.RunBookingSaga), bookingRequest,
                new StartOrchestrationOptions(bookingRequest.BookingId));
            logger.LogInformation("Started booking saga with ID = '{bookingId}'.", bookingRequest.BookingId);
        }
        else
        {
            logger.LogInformation("Booking '{bookingId}' already exists; not starting a second saga.", bookingRequest.BookingId);
        }

        return await client.CreateCheckStatusResponseAsync(request, bookingRequest.BookingId);
    }

    [Function(nameof(GetBookingStatus))]
    public static async Task<HttpResponseData> GetBookingStatus(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "bookings/{bookingId}")] HttpRequestData request,
        [DurableClient] DurableTaskClient client,
        string bookingId)
    {
        var instance = await client.GetInstanceAsync(bookingId, getInputsAndOutputs: true);
        if (instance is null)
            return await WriteJson(request, HttpStatusCode.NotFound, new { error = "booking not found", bookingId });

        var response = new BookingStatusResponse(
            instance.InstanceId,
            instance.RuntimeStatus.ToString(),
            instance.SerializedCustomStatus,
            instance.SerializedOutput,
            instance.CreatedAt,
            instance.LastUpdatedAt);
        return await WriteJson(request, HttpStatusCode.OK, response);
    }

    private static async Task<HttpResponseData> WriteJson<T>(HttpRequestData request, HttpStatusCode statusCode, T payload)
    {
        var response = request.CreateResponse(statusCode);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await JsonSerializer.SerializeAsync(response.Body, payload, JsonOptions);
        return response;
    }
}
