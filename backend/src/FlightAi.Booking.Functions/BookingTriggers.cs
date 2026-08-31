using System.Net;
using System.Text.Json;
using FlightAi.Booking.Functions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
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
        var body = await JsonSerializer.DeserializeAsync<BookingRequest>(request.Body, JsonOptions);
        if (body is null)
            return await WriteJson(request, HttpStatusCode.BadRequest, new { error = "Request body is required." });

        var existing = await client.GetInstanceAsync(body.BookingId, getInputsAndOutputs: false);
        if (existing is null)
        {
            await client.ScheduleNewOrchestrationInstanceAsync(
                nameof(BookingOrchestrator.RunBookingSaga), body,
                new StartOrchestrationOptions(body.BookingId));
            logger.LogInformation("Started booking saga with ID = '{bookingId}'.", body.BookingId);
        }
        else
        {
            logger.LogInformation("Booking '{bookingId}' already exists; not starting a second saga.", body.BookingId);
        }

        return await client.CreateCheckStatusResponseAsync(request, body.BookingId);
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
