using System.Text.Json;
using System.Threading.Channels;
using FlightAi.Agents;
using FlightAi.Core.Offers;
using FlightAi.Core.Pricing;
using FlightAi.Core.Ranking;
using FlightAi.Core.Suppliers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();
app.Urls.Add("http://localhost:5179");
app.UseCors();

var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

// Stream ranked results the moment ranking finishes, then fill in the explanation asynchronously —
// each stage below reaches the client the instant it is ready, not all at once at the end.
app.MapGet("/api/search/stream", async (HttpContext ctx, string q, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(q))
    {
        ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
        await ctx.Response.WriteAsync("Query string 'q' is required.");
        return;
    }

    ctx.Response.Headers.ContentType = "text/event-stream";
    ctx.Response.Headers.CacheControl = "no-cache";
    ctx.Response.Headers.Append("X-Accel-Buffering", "no");

    async Task Emit(string eventName, object data)
    {
        await ctx.Response.WriteAsync($"event: {eventName}\n", cancellationToken);
        await ctx.Response.WriteAsync($"data: {JsonSerializer.Serialize(data, json)}\n\n", cancellationToken);
        await ctx.Response.Body.FlushAsync(cancellationToken);
    }

    try
    {
        // Stage 1 — intent parsing: natural language in, a typed, schema-validated SearchRequest out.
        var intentAgent = IntentAgentFactory.CreateOffline();
        var intentResponse = await intentAgent.RunAsync<SearchRequest>(q, cancellationToken: cancellationToken);
        var searchRequest = intentResponse.Result;
        await Emit("parsed-intent", searchRequest);

        // Stage 2 — supplier fan-out, streamed per supplier as each one lands rather than waiting
        // for the slowest one, so one stalled supplier never stalls the whole search.
        var budget = new LookToBookBudget(maxCallsPerSupplierPerSession: 10);
        var orchestrator = new SupplierFanOutOrchestrator(
            [new MockGdsConnector(), new MockNdcConnector()], budget);

        var supplierEvents = Channel.CreateUnbounded<SupplierResult>();
        var fanOutTask = orchestrator.SearchAsync(
            searchRequest,
            onSupplierResult: r => supplierEvents.Writer.TryWrite(r),
            cancellationToken: cancellationToken);
        _ = fanOutTask.ContinueWith(_ => supplierEvents.Writer.TryComplete(), TaskScheduler.Default);

        await foreach (var supplierResult in supplierEvents.Reader.ReadAllAsync(cancellationToken))
        {
            await Emit("supplier-result", new
            {
                supplierResult.SupplierId,
                supplierResult.Succeeded,
                offerCount = supplierResult.Offers.Count,
                elapsedMs = supplierResult.Elapsed.TotalMilliseconds,
                supplierResult.Error
            });
        }

        var fanOutResult = await fanOutTask;

        // Stage 3 — deterministic ranking: a scoring function, not a chat.
        var ranked = new OfferScorer().Rank(fanOutResult.Offers);
        await Emit("ranked-offers", ranked.Select((s, i) => new
        {
            rank = i + 1,
            s.Offer.OfferId,
            s.Offer.SupplierId,
            carrier = s.Offer.Segments[0].Carrier,
            price = s.Offer.TotalPrice,
            s.Offer.Currency,
            stops = s.Offer.StopCount,
            durationMinutes = (int)s.Offer.TotalDuration.TotalMinutes,
            refundable = s.Offer.FareRules.Refundable,
            score = Math.Round(s.Score, 3)
        }));

        // Stage 4 — the explanation agent, still bound by the price-integrity rule: it only ever
        // sees opaque tokens, and only PriceReferenceStore turns one into a digit.
        var priceStore = new PriceReferenceStore(ranked);
        var explanationAgent = ExplanationAgentFactory.CreateOffline();
        var prompt = ExplanationAgentFactory.BuildPrompt(priceStore.BuildAgentContext(), priceStore.BuildComparisonTokens());
        var explanationResponse = await explanationAgent.RunAsync(prompt, cancellationToken: cancellationToken);
        var rendered = ExplanationPlaceholderRenderer.Render(explanationResponse.Text, priceStore);

        await Emit("explanation", new
        {
            text = rendered.Text,
            raw = explanationResponse.Text,
            isClean = rendered.IsClean
        });

        await Emit("done", new { });
    }
    catch (Exception ex)
    {
        await Emit("error", new { message = ex.Message });
    }
});

app.Run();
