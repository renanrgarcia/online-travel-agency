using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace FlightAi.Booking.Functions;

/// <summary>
/// Task 14: the smallest possible Durable orchestration, proving the local dev loop (Azurite +
/// Core Tools + checkpointing + idempotent instance IDs) before any real saga logic exists.
/// <para>
/// TODO(remove after task 15): scaffolding, not part of the final solution. Task 15's real
/// <c>BookingOrchestrator</c> exercises the same platform guarantees (checkpointing, idempotent
/// instance IDs) with actual saga logic, so this file and its <c>/api/trivial/{id}</c> route should
/// be deleted once that lands rather than left behind as dead weight.
/// </para>
/// </summary>
public static class TrivialOrchestrator
{
    /// <summary>
    /// The timer creates an observable in-flight window (task 14 E5): a zero-duration orchestration
    /// completes before a host restart could ever land mid-run, so there'd be nothing to prove
    /// checkpointing resumes. The delay is replayed like any other awaited call -- it doesn't re-wait
    /// on resume if it already fired before the restart.
    /// </summary>
    [Function(nameof(RunOrchestrator))]
    public static async Task<string> RunOrchestrator([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        await context.CreateTimer(TimeSpan.FromSeconds(10), CancellationToken.None);
        return "trivial orchestration complete";
    }

    [Function(nameof(StartTrivialOrchestrator))]
    public static async Task<HttpResponseData> StartTrivialOrchestrator(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "trivial/{instanceId}")] HttpRequestData request,
        [DurableClient] DurableTaskClient client,
        string instanceId,
        FunctionContext executionContext)
    {
        var logger = executionContext.GetLogger(nameof(StartTrivialOrchestrator));

        var existing = await client.GetInstanceAsync(instanceId);
        if (existing is null)
        {
            await client.ScheduleNewOrchestrationInstanceAsync(
                nameof(RunOrchestrator), input: null, options: new StartOrchestrationOptions(instanceId));
            logger.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);
        }
        else
        {
            logger.LogInformation("Instance '{instanceId}' already exists; not starting a second one.", instanceId);
        }

        return await client.CreateCheckStatusResponseAsync(request, instanceId);
    }
}
