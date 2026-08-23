# The AI layer: intent parsing and explanation

`FlightAi.Agents/` is the only project in the solution that touches a language model. It produces
exactly two kinds of output, and nothing else:

1. A typed object (`IntentAgentFactory`) — natural language in, a schema-validated `SearchRequest` out.
2. Prose built from opaque tokens (`ExplanationAgentFactory`) — see `02-price-integrity.md` for why the
   tokens matter.

## `IntentAgentFactory`

Natural language in, a typed, schema-validated `SearchRequest` out, validated against a schema before
it ever reaches a supplier. `Microsoft.Agents.AI`'s `RunAsync<T>` is what does the "validated against a
schema" part — `IntentAgentFactory` just wires an agent up to call it. Nothing downstream of this point
ever reads free text again; every later stage of the pipeline works with the typed `SearchRequest`.

## `ExplanationAgentFactory`

The agent is handed only opaque tokens — never a price, a duration, or a stop count — and writes prose
that references them. Rendering the tokens into real digits happens afterward, in
`ExplanationPlaceholderRenderer`, which this agent has no access to and no knowledge of. The separation
matters: the agent that generates text and the code that resolves numbers are different components with
different trust levels, and the agent literally cannot leak a number it was never given.

## `OfflineChatClient`

A deterministic stand-in for a real model-backed `IChatClient` (Azure OpenAI, Microsoft Foundry,
Anthropic via Foundry, or any OpenAI-compatible endpoint), so the whole pipeline runs with `dotnet run`
and no API key. Both agent factories construct their `AIAgent` from a plain `IChatClient` — the offline
path just supplies a mock implementation of that interface instead of a real one.

## Swapping in a real model

Nothing above `OfflineChatClient` changes. Both factories already accept a plain `IChatClient`; the
offline path just calls them with a mock. For a real provider, the shape is:

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

Verify exact package and method names against current provider docs before using this — provider
adapter packages move fast. The model choice and the agent-framework choice are genuinely separate
decisions; this codebase doesn't care which model sits behind the `IChatClient` you hand it. Any
provider exposing an OpenAI-compatible chat completions endpoint (including free tiers like Gemini's)
plugs in the same way — see `08-package-versions.md` for the exact NuGet packages this was verified
against.
