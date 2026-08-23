# Flight AI samples

A small, runnable .NET 10 solution demonstrating an AI-assisted flight search and booking architecture
— deterministic ranking and pricing logic with two narrowly-scoped AI agents layered on top, plus a
booking saga with compensation. Not slides about it, the actual code, runnable and tested.

```bash
dotnet build            # 0 warnings, 0 errors
dotnet test             # 17 tests, all green
dotnet run --project src/FlightAi.Demo
```

No API key needed. The two agents run against a fully offline mock model (`OfflineChatClient`) so the
whole pipeline — intent parsing, supplier fan-out, ranking, explanation — runs end to end with nothing
but the .NET SDK. See **"Swapping in a real model"** below for the one-line change to point it at Azure
OpenAI, Microsoft Foundry, or Anthropic.

There are now three ways to see this system run: the console demo above, a live HTTP API with a React
front end (search), and an Azure Durable Functions saga (booking). All three are covered below.

## What's here

```
src/
  FlightAi.Core/      Domain + deterministic logic. No AI dependency at all.
    Offers/            The canonical offer model and the typed SearchRequest.
    Suppliers/         ISupplierConnector, fan-out orchestrator, look-to-book budget, circuit breaker.
    Ranking/           OfferScorer — the weighted scoring function.
    Pricing/           PriceReferenceStore + ExplanationPlaceholderRenderer — the price-integrity boundary.
  FlightAi.Agents/     The AI layer. Depends on Microsoft.Agents.AI + Microsoft.Extensions.AI.Abstractions.
    IntentAgentFactory.cs        NL -> typed SearchRequest via RunAsync<T>.
    ExplanationAgentFactory.cs   Ranked offers -> prose, using opaque tokens instead of numbers.
    OfflineChatClient.cs         The offline IChatClient stand-in — swap this out for the real thing.
  FlightAi.Demo/       Console app wiring all four pieces into one run.
  FlightAi.Api/        Minimal API. GET /api/search/stream — Server-Sent Events, one per pipeline stage.
  FlightAi.Booking.Functions/   Azure Durable Functions saga for the booking flow.
tests/
  FlightAi.Tests/      xUnit. Determinism, price-integrity, budget, circuit-breaker, timeout.
web/                  React + TypeScript + Vite front end for FlightAi.Api and the booking saga.
```

## The centerpiece: the price-integrity boundary

The sharpest rule in this whole system: a language model may never author a number that reaches the
user, and that must be enforced structurally, not by prompt discipline. `FlightAi.Core/Pricing` is that
rule as code:

1. `PriceReferenceStore` hands the explanation agent **tokens**, never numbers — `{{PRICE_OFF123}}`,
   `{{DURATION_OFF123}}`, `{{PRICE_DELTA_A_vs_B}}`. There is no `MARGIN_` token at all, so your
   commercial margin has no path to leak into a traveller-facing explanation, hallucinated or not.
2. The model writes prose around those tokens.
3. `ExplanationPlaceholderRenderer` is the *only* code allowed to turn a token into a digit, and it
   always resolves from the store — never from anything the model typed.
4. It also checks the model's raw text for any digit sitting **outside** a token at all. Telling the
   model "never write a number yourself" in the system prompt is a request, not a guarantee — a model
   can ignore it. So `FlightAi.Tests/ExplanationPlaceholderRendererTests.cs` includes a test where the
   mock "model" ignores its instructions and types `$499` directly into its prose, and asserts the
   renderer catches it anyway.

Run `dotnet test --filter ExplanationPlaceholderRenderer` to see all six of those checks pass on their
own, or just read that one test file — it's the highest-value ten minutes in this repo.

## Running the API + React app (search)

Two processes:

```bash
dotnet run --project src/FlightAi.Api      # listens on http://localhost:5179
cd web && npm install && npm run dev        # listens on http://localhost:5173
```

Open `http://localhost:5173`. Submitting a search opens one `EventSource` connection to
`GET /api/search/stream?q=...` and renders four Server-Sent Events as they land — `parsed-intent`, one
`supplier-result` per supplier (in true completion order, not declaration order — watch the two chips
resolve independently), `ranked-offers`, then `explanation`. Ranked results reach the screen before the
explanation prompt has even been sent — that ordering is the whole point of streaming per stage instead
of waiting for everything to finish at once.

The explanation card has a **"show model's raw output"** toggle. Flip it and the `{{PRICE_...}}` /
`{{DURATION_DELTA_...}}` tokens the model actually wrote are highlighted in place of the rendered
numbers — the fastest way to see the price-integrity boundary from the earlier section as something
visible instead of something you have to take on faith.

Clicking **Book** on an offer opens the booking flow, which talks to the Durable Functions saga below —
start that too if you want the button to do anything.

## Running the booking saga (Azure Durable Functions)

This one needs two more local pieces, both free:

```bash
npm install -g azurite azure-functions-core-tools@4   # once
azurite --silent --location ./azurite-data --skipApiVersionCheck &
func start --port 7071 --prefix src/FlightAi.Booking.Functions   # or: cd there first, then `func start`
```

The `--skipApiVersionCheck` flag matters — see "Bugs and friction this caught" below. Once it's running:

```bash
# Happy path
curl -X POST http://localhost:7071/api/bookings -H "Content-Type: application/json" -d '{
  "bookingId": "demo-001", "offerId": "NDC-abc123", "travellerEmail": "t@example.com",
  "amount": 791.00, "currency": "USD", "paymentMethodToken": "tok_test"
}'
curl http://localhost:7071/api/bookings/demo-001   # poll until runtimeStatus is "Completed"

# Compensation path — an offerId containing FAIL-TICKET fails ticketing on purpose
curl -X POST http://localhost:7071/api/bookings -H "Content-Type: application/json" -d '{
  "bookingId": "demo-002", "offerId": "NDC-FAIL-TICKET-xyz", "travellerEmail": "t@example.com",
  "amount": 650.00, "currency": "USD", "paymentMethodToken": "tok_test"
}'
curl http://localhost:7071/api/bookings/demo-002
```

`BookingOrchestrator.RunBookingSaga` (in `FlightAi.Booking.Functions`) is a
checkpointed state machine: authorize payment → create order → issue ticket → send confirmation, with
each of the first three steps wired to a compensating action (void the payment, cancel the order) if a
later step fails. `bookingId` is also the orchestration instance ID, which is the idempotency mechanism
— retrying a `POST` with the same `bookingId` lands on the same saga instance rather than authorizing
payment twice. An offer ID containing `FAIL-ORDER` or `FAIL-TICKET` deterministically fails that step
(same convention as the mock supplier connectors), so the compensation path is reproducible on demand
instead of left to chance. I ran both paths against a live host with Azurite standing in for Azure
Storage before calling this finished — see below.

## Bugs and friction this caught

Worth knowing about, because they only show up when you actually run the code rather than read it:

- **The token regex only allowed `[A-Za-z0-9_]`, but offer IDs contain hyphens.** Tokens silently failed
  to match at all, and — because a token that never matches the pattern never reaches the "unresolved"
  list either — the renderer reported the output as clean while the traveller-facing text still had raw
  `{{...}}` placeholders in it. Fixed by widening the character class and adding a `HasUnmatchedBraces`
  check that flags *any* literal `{{` surviving rendering, matched or not.
- **Currency formatting used the ambient `CultureInfo.CurrentCulture`.** On a machine whose OS region
  uses a comma decimal separator, `500.00 USD` silently rendered as `500,00 USD` — no error, no
  warning, just a differently-shaped price string depending on which machine happened to run the
  process. Fixed with explicit `CultureInfo.InvariantCulture` in `PriceReferenceStore`, and the same
  pin at the top of `Program.cs` and `FlightAi.Api/Program.cs`.
- **`WriteAsJsonAsync` already sets `Content-Type`; a manual `Headers.Add("Content-Type", ...)` before
  it threw `FormatException: Cannot add value because header 'Content-Type' does not support multiple
  values`.** `GetBookingStatus` 500'd on every call until this line was deleted. Caught by actually
  calling the endpoint with `curl`, not by `dotnet build`, which saw nothing wrong.
- **Azurite 3.36 rejects the API version the current `Azure.Storage.Blobs` client sends
  (`2026-02-06`)**, so the Durable Task orchestration listener failed to start with a storage error that
  has nothing to do with any code in this repo. The fix is the flag in the command above —
  `--skipApiVersionCheck` — which Azurite's own error message names directly. Worth knowing before you
  spend time suspecting your own code for a tooling-version mismatch.

The first two are the classic "silent, no error in the logs" failure shape — nothing crashes, nothing
logs a warning, the output is just wrong. The third is the same failure shape one layer up the stack.
The fourth is not a code bug at all — it's an undocumented tooling/version mismatch, the kind of hidden
edge case that only shows up the first time you actually run the saga against a local emulator.

## Swapping in a real model

Nothing above `OfflineChatClient` changes. `IntentAgentFactory.Create` and `ExplanationAgentFactory.Create`
already take a plain `IChatClient` — the offline path just calls them with a mock. For Azure OpenAI or
Microsoft Foundry, the shape is (verify the exact package and method names against current Microsoft
Learn docs before using this — provider adapter packages move fast and this is illustrative, not
compiled/tested code the way everything else in this repo is):

```csharp
// dotnet add package Azure.AI.OpenAI
// dotnet add package Microsoft.Extensions.AI.OpenAI
var azureClient = new AzureOpenAIClient(
    new Uri("https://<your-resource>.openai.azure.com"),
    new DefaultAzureCredential());
IChatClient chatClient = azureClient.GetChatClient("<deployment-name>").AsIChatClient();

var intentAgent = IntentAgentFactory.Create(chatClient);
var explanationAgent = ExplanationAgentFactory.Create(chatClient);
```

Foundry now serves Anthropic models alongside OpenAI's — same `IChatClient` shape, just a
different deployment. The model choice and the agent-framework choice are genuinely separate decisions;
this codebase doesn't care which model sits behind the `IChatClient` you hand it.

## What's deliberately not here

- **Redis-backed offer/semantic caching.** The two-cache design is real but a Redis
  dependency doesn't belong in a `dotnet run`-and-go sample; `LookToBookBudget` and the circuit breaker
  are the pieces that don't need external infrastructure to demonstrate.
- **Real supplier wire formats.** `MockGdsConnector` / `MockNdcConnector` / `MockLccConnector` return the
  canonical `Offer` shape directly — they exist to prove the fan-out orchestrator's contract (timeout,
  budget, partial results), not to parse actual Amadeus/Sabre/Duffel JSON.
- **A resilience library.** `SupplierCircuitBreaker` is hand-rolled on purpose, so its behavior is
  readable in one file. Reach for Polly in a real service instead of this.

## Package versions used

`Microsoft.Agents.AI` 1.18.0, `Microsoft.Agents.AI.Abstractions` 1.18.0 (transitive),
`Microsoft.Extensions.AI.Abstractions` 10.9.0, targeting `net10.0`. The exact API surface —
`ChatClientExtensions.AsAIAgent`, `AIAgent.RunAsync<T>` returning `AgentResponse<T>.Result`,
`AIAgent.RunAsync` returning `AgentResponse.Text` — was confirmed by reflecting on the installed
assemblies while building this, not guessed from documentation, since the framework only reached
general availability in April 2026 and public examples are still thin.

`FlightAi.Booking.Functions` targets `Microsoft.Azure.Functions.Worker` 2.52.0,
`Microsoft.Azure.Functions.Worker.Sdk` 2.1.0, `Microsoft.Azure.Functions.Worker.Extensions.DurableTask`
1.18.0 (pulling in `Microsoft.DurableTask.Abstractions`/`.Client` 1.24.1), Azure Functions Core Tools
4.13.0. Same policy as above: the orchestration context, client, and trigger-attribute surface —
`TaskOrchestrationContext.CallActivityAsync`, `DurableTaskClient.ScheduleNewOrchestrationInstanceAsync`,
`StartOrchestrationOptions`, `CreateCheckStatusResponseAsync` — was confirmed by reflecting on the
installed assemblies, then the whole saga was run against a live host with Azurite standing in for
Azure Storage, both the happy path and the compensation path, before this was called done.

`web/` is Vite 8 + React 19 + TypeScript 6, scaffolded with `npm create vite@latest -- --template
react-ts` and otherwise dependency-free — no state library, no UI kit, no CSS framework. `npm run build`
type-checks with `tsc -b` before bundling.
