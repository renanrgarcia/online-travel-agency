using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace FlightAi.Booking.Functions;

public static class BookingTriggers
{
    [Function(nameof(StartBooking))]
    public static async Task<HttpResponseData> StartBooking(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "bookings")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        FunctionContext executionContext)
    {
        var logger = executionContext.GetLogger(nameof(StartBooking));
        var request = await req.ReadFromJsonAsync<BookingRequest>();

        if (request is null || string.IsNullOrWhiteSpace(request.BookingId) || string.IsNullOrWhiteSpace(request.OfferId))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("bookingId, offerId, travellerEmail, amount, currency and paymentMethodToken are required.");
            return bad;
        }

        // Idempotency: the booking ID IS the orchestration instance ID. A
        // retried or duplicated request with the same booking ID lands on the same saga instance
        // instead of authorizing payment twice.
        var instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(BookingOrchestrator.RunBookingSaga),
            request,
            new StartOrchestrationOptions(InstanceId: request.BookingId));

        logger.LogInformation("Started booking saga {InstanceId} for offer {OfferId}", instanceId, request.OfferId);

        return await client.CreateCheckStatusResponseAsync(req, instanceId);
    }

    [Function(nameof(GetBookingStatus))]
    public static async Task<HttpResponseData> GetBookingStatus(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "bookings/{bookingId}")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        string bookingId)
    {
        var metadata = await client.GetInstanceAsync(bookingId, getInputsAndOutputs: true);

        if (metadata is null)
        {
            var notFound = req.CreateResponse(HttpStatusCode.NotFound);
            await notFound.WriteStringAsync($"No booking found with id '{bookingId}'.");
            return notFound;
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new
        {
            bookingId,
            runtimeStatus = metadata.RuntimeStatus.ToString(),
            customStatus = metadata.SerializedCustomStatus,
            output = metadata.SerializedOutput,
            createdAt = metadata.CreatedAt,
            lastUpdatedAt = metadata.LastUpdatedAt
        });
        return response;
    }
}
