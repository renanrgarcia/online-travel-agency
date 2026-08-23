using System.Globalization;
using FlightAi.Agents;
using FlightAi.Core.Offers;
using FlightAi.Core.Pricing;
using FlightAi.Core.Ranking;
using FlightAi.Core.Suppliers;

// Pinned so this console output reads the same on every machine, regardless of OS region settings —
// see the FormatMoney comment in PriceReferenceStore for the bug this exact gap caused during dev.
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

Section("1. Intent agent — natural language -> typed SearchRequest");

const string utterance = "Two adults, Lisbon to São Paulo, first week of December, no red-eyes, aisle seats";
Console.WriteLine($"Utterance : \"{utterance}\"");

var intentAgent = IntentAgentFactory.CreateOffline();
var intentResponse = await intentAgent.RunAsync<SearchRequest>(utterance);
var searchRequest = intentResponse.Result;

Console.WriteLine(
    $"Parsed    : {searchRequest.Origin} -> {searchRequest.Destination}, " +
    $"{searchRequest.DepartureDate:yyyy-MM-dd}, {searchRequest.Travellers.Adults} adult(s), " +
    $"{searchRequest.Cabin}, avoidRedEyes={searchRequest.Preferences.AvoidRedEyes}, " +
    $"seat={searchRequest.Preferences.SeatPreference ?? "none"}");

Section("2. Supplier fan-out — timeouts, circuit breaker, look-to-book budget");
Console.WriteLine("Four simulated searches against the same session, the way a poorly-bounded");
Console.WriteLine("exploratory agent might behave. Budget = 3 calls per");
Console.WriteLine("supplier; the LCC connector is deliberately unreliable.\n");

var resilienceBudget = new LookToBookBudget(maxCallsPerSupplierPerSession: 3);
var resilienceOrchestrator = new SupplierFanOutOrchestrator(
    connectors: [new MockGdsConnector(), new MockNdcConnector(), new MockLccConnector()],
    budget: resilienceBudget,
    perSupplierTimeout: TimeSpan.FromMilliseconds(1200),
    circuitFailureThreshold: 2,
    circuitOpenDuration: TimeSpan.FromSeconds(10));

for (var session = 1; session <= 4; session++)
{
    var result = await resilienceOrchestrator.SearchAsync(searchRequest);
    Console.WriteLine($"session {session} — {(result.IsPartial ? "PARTIAL results" : "complete")}");

    foreach (var r in result.PerSupplier)
    {
        var status = r switch
        {
            { Succeeded: true } => $"ok — {r.Offers.Count} offer(s) in {r.Elapsed.TotalMilliseconds:0}ms",
            { BudgetSkipped: true } => $"SKIPPED — {r.Error}",
            { CircuitOpen: true } => "SKIPPED — circuit open, supplier not even called",
            _ => $"FAILED — {r.Error}"
        };
        Console.WriteLine($"   {r.SupplierId,-14} {status}");
    }
}

Console.WriteLine($"\nBudget spent this session: {string.Join(", ", resilienceBudget.Snapshot().Select(kv => $"{kv.Key}={kv.Value}"))}");
Console.WriteLine($"Total shopping calls: {resilienceBudget.TotalCalls()} (the counter that belongs on the same dashboard as p95 latency)");

Section("3. Deterministic ranking — a scoring function, not a chat");

var rankingOrchestrator = new SupplierFanOutOrchestrator(
    connectors: [new MockGdsConnector(), new MockNdcConnector()],
    budget: new LookToBookBudget(maxCallsPerSupplierPerSession: 10));
var rankingFanOut = await rankingOrchestrator.SearchAsync(searchRequest);

var ranked = new OfferScorer().Rank(rankingFanOut.Offers);
for (var i = 0; i < ranked.Count; i++)
{
    var s = ranked[i];
    var duration = $"{(int)s.Offer.TotalDuration.TotalHours}h{s.Offer.TotalDuration.Minutes:00}m";
    Console.WriteLine(
        $"   #{i + 1} {s.Offer.SupplierId,-12} {s.Offer.TotalPrice,7:0.00} {s.Offer.Currency}  " +
        $"{s.Offer.StopCount} stop(s)  {duration}  score={s.Score:0.000}");
}

Section("4. Explanation agent + the price-integrity boundary");
Console.WriteLine("The model gets opaque tokens for price/duration/stops — never the numbers.");
Console.WriteLine("Only PriceReferenceStore is allowed to turn a token into a digit.\n");

var priceStore = new PriceReferenceStore(ranked);
var explanationAgent = ExplanationAgentFactory.CreateOffline();
var prompt = ExplanationAgentFactory.BuildPrompt(priceStore.BuildAgentContext(), priceStore.BuildComparisonTokens());
var explanationResponse = await explanationAgent.RunAsync(prompt);

Console.WriteLine("Model's raw output (placeholders, zero digits):");
Console.WriteLine($"  {explanationResponse.Text}\n");

var rendered = ExplanationPlaceholderRenderer.Render(explanationResponse.Text, priceStore);
Console.WriteLine("Rendered for the traveller (substituted from the store, not from the model):");
Console.WriteLine($"  {rendered.Text}\n");

Console.WriteLine(rendered.IsClean
    ? "  All tokens resolved — nothing the model wrote reached the user as a raw, model-authored number."
    : $"  WARNING — unresolved tokens: {string.Join(", ", rendered.UnresolvedTokens)}");

return;

static void Section(string title)
{
    Console.WriteLine();
    Console.WriteLine(new string('=', title.Length));
    Console.WriteLine(title);
    Console.WriteLine(new string('=', title.Length));
}
