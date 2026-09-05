using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using FlightAi.Agents.Models.Explanation;
using FlightAi.Agents.Models.Intent;
using FlightAi.Agents.Services.Explanation;
using FlightAi.Agents.Services.Intent;
using FlightAi.Core.Models.Offers;
using FlightAi.Core.Models.Ranking;
using FlightAi.Core.Services.Pricing;
using FlightAi.Core.Services.Ranking;
using FlightAi.Core.Services.Suppliers;
using Microsoft.Extensions.AI;

namespace FlightAi.Api;

/// <summary>
/// Streams the real search pipeline as Server-Sent Events: intent parsing, supplier fan-out, ranking,
/// explanation — one event per stage, emitted as each stage actually finishes. See
/// docs/reference/06-api-sse-contract.md and docs/features/01-backend/tasks/13-search-api-sse-full-pipeline.md.
/// <para>
/// Rendering happens here, server-side, always — the browser never receives a token and never learns
/// the token vocabulary exists. A rendering guard violation degrades only the <c>explanation</c> event
/// (<see cref="ExplanationPayload.IsClean"/> false); offers already streamed stay useful.
/// </para>
/// </summary>
public static class SearchPipeline
{
    // Enums (SupplierStatus) as their name, not the default numeric value -- a client has no reason to
    // know or care that Failed happens to be 2, and the number would silently renumber itself if the
    // enum's declaration order ever changed.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Explains only the top-ranked offers, not the full result set — a locked decision, not
    /// specified by task 13 itself. Three is enough for a traveller to compare without asking the
    /// explanation agent to write about offers nobody will read past.</summary>
    private const int ExplainedOfferCount = 3;

    /// <summary>Caps how many offers the <c>ranked-offers</c> event actually carries (task 25 follow-up)
    /// — with only mock connectors this never mattered (a handful of offers total), but a real supplier
    /// can return dozens to hundreds. `Rank` still reflects each offer's true position among every offer
    /// found, not a position within just this capped slice, so a future "show more" affordance can page
    /// in rank order without renumbering anything already shown. Every supplier's own true offer count
    /// still reaches the client via each `supplier-result` event, uncapped — this only bounds the
    /// ranked list's own payload size.</summary>
    private const int DisplayedOfferCount = 10;

    public static async IAsyncEnumerable<SseItem<string>> RunAsync(
        string query,
        IntentAgent intentAgent,
        SupplierFanOutOrchestrator supplierOrchestrator,
        IChatClient chatClient,
        PriceAssertionService priceAssertionService,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        IntentResult? intentResult = null;
        var aiUnavailable = false;
        try
        {
            intentResult = await intentAgent.ParseAsync(query, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            yield break;
        }
        catch (Exception)
        {
            aiUnavailable = true;
        }

        if (aiUnavailable)
        {
            yield return Event("error", new
            {
                code = "ai-unavailable",
                message = "The AI service is temporarily unavailable. Please start a new search later.",
                rawModelResponse = (string?)null,
            });
            yield break;
        }

        if (!intentResult!.Success)
        {
            yield return Event("error", new
            {
                code = intentResult.Code,
                message = intentResult.FailureReason,
                rawModelResponse = intentResult.RawModelResponse,
            });
            yield break;
        }

        var request = intentResult.Request!;
        yield return Event("parsed-intent", request);

        var allOffers = new List<Offer>();
        await foreach (var (offers, report) in supplierOrchestrator.SearchStreamingAsync(request, cancellationToken))
        {
            allOffers.AddRange(offers);
            yield return Event("supplier-result", report);
        }

        // Not sorted here on purpose: SearchStreamingAsync yields in real completion order, but
        // OfferScorer.Rank orders by score and breaks ties on OfferId, so its output is a total order
        // independent of what order it received its input in.
        var scorable = allOffers
            .Select(offer => new ScorableOffer(offer.OfferId, offer.Price, offer.Duration, offer.Stops, offer.Margin))
            .ToList();
        var ranked = OfferScorer.Rank(scorable, ScoringWeights.Default);
        var offersById = allOffers.ToDictionary(offer => offer.OfferId);

        var rankedViews = ranked
            .Select((scored, index) => (Scored: scored, Rank: index + 1))
            .Take(DisplayedOfferCount)
            .Select(entry =>
            {
                var offer = offersById[entry.Scored.OfferId];
                return new RankedOfferView(
                    Rank: entry.Rank,
                    OfferId: offer.OfferId,
                    Price: offer.Price,
                    Currency: offer.Currency,
                    DurationMinutes: (int)offer.Duration.TotalMinutes,
                    Stops: offer.Stops,
                    Refundable: offer.Refundable,
                    Score: OfferScorer.Score(entry.Scored, ScoringWeights.Default),
                    PriceAssertion: priceAssertionService.Issue(offer.OfferId, offer.Price, offer.Currency),
                    OriginAirport: offer.OriginAirport,
                    DestinationAirport: offer.DestinationAirport);
            })
            .ToList();
        yield return Event("ranked-offers", rankedViews);

        if (ranked.Count == 0)
        {
            // No offers to explain -- every connector failed, timed out, or (task 13 E8) was cancelled
            // before finishing. Deterministic, no model call: an empty prompt isn't just pointless
            // here, Microsoft.Agents.AI's RunAsync rejects a blank message outright.
            yield return Event("explanation", new ExplanationPayload(Text: "No offers were found for this search.", Raw: "", IsClean: true));
            yield break;
        }

        var store = new PriceReferenceStore(request.Language);
        var explainedOffers = ranked.Take(ExplainedOfferCount).Select(scored => offersById[scored.OfferId]).ToList();
        var comparisons = ComparisonFacts.Compute(store, explainedOffers);
        var tokenizedOffers = explainedOffers
            .Select(offer => Tokenize(store, offer, comparisons[offer.OfferId]))
            .ToList();

        var explanationAgent = ExplanationAgentFactory.Create(chatClient, request.Language);
        var raw = await explanationAgent.ExplainAsync(tokenizedOffers, cancellationToken);
        var rendered = new ExplanationPlaceholderRenderer(store).Render(raw);
        var isClean = rendered.Success && rendered.Violations.Count == 0;

        yield return Event("explanation", new ExplanationPayload(Text: isClean ? rendered.Text : "", Raw: raw, IsClean: isClean));
    }

    private static TokenizedOffer Tokenize(PriceReferenceStore store, Offer offer, OfferComparison comparison) => new(
        offer.OfferId,
        store.RegisterPrice(offer.OfferId, offer.Price, offer.Currency),
        store.RegisterDuration(offer.OfferId, offer.Duration),
        store.RegisterStops(offer.OfferId, offer.Stops),
        store.RegisterRefundable(offer.OfferId, offer.Refundable),
        comparison.PriceDeltaToken,
        comparison.DurationDeltaToken,
        comparison.SuperlativeTokens);

    private static SseItem<string> Event<T>(string eventType, T payload) =>
        new(JsonSerializer.Serialize(payload, JsonOptions), eventType);
}
