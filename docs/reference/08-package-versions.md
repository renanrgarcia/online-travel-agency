# Verified package versions and API surfaces

These were confirmed by reflecting on the actual installed assemblies while building this system, not
guessed from documentation — Microsoft Agent Framework only reached general availability in April 2026
and public examples were still thin at the time. Worth re-verifying against current NuGet before
rebuilding, since these frameworks move fast, but this is a known-good starting point rather than a
blind guess.

## AI layer

- `Microsoft.Agents.AI` 1.18.0
- `Microsoft.Agents.AI.Abstractions` 1.18.0 (transitive dependency of the above)
- `Microsoft.Extensions.AI.Abstractions` 10.9.0
- Target framework: `net10.0`

Confirmed API surface:
- `ChatClientExtensions.AsAIAgent(this IChatClient, instructions:, name:, description:, ...)` — builds
  an `AIAgent` from a plain `IChatClient`.
- `AIAgent.RunAsync<T>(message)` → `Task<AgentResponse<T>>`, with the typed result on
  `AgentResponse<T>.Result`.
- `AIAgent.RunAsync(message)` → `Task<AgentResponse>`, with the text on `AgentResponse.Text`.

## Booking Functions app

- `Microsoft.Azure.Functions.Worker` 2.52.0
- `Microsoft.Azure.Functions.Worker.Sdk` 2.1.0
- `Microsoft.Azure.Functions.Worker.Extensions.DurableTask` 1.18.0 (pulls in
  `Microsoft.DurableTask.Abstractions` / `.Client` 1.24.1)
- Azure Functions Core Tools 4.13.0

Confirmed API surface:
- `TaskOrchestrationContext.CallActivityAsync<T>(name, input, options)` — inside the orchestrator.
- `DurableTaskClient.ScheduleNewOrchestrationInstanceAsync(name, input, options)` — starts a new saga
  instance; `StartOrchestrationOptions.InstanceId` is the idempotency mechanism (see
  `07-booking-saga.md`).
- `DurableTaskClientExtensions.CreateCheckStatusResponseAsync(client, request, instanceId)` — the
  standard 202-Accepted-with-status-URL HTTP response helper.

## Local dev tooling

- Azurite 3.36 (storage emulator) — needs `--skipApiVersionCheck`, see `09-lessons-learned.md`.
- Both installable via `npm install -g azurite azure-functions-core-tools@4`.

## Frontend

- Vite 8, React 19, TypeScript 6.
- Scaffolded with `npm create vite@latest -- --template react-ts` and kept dependency-free otherwise —
  no state library, no UI kit, no CSS framework. `npm run build` type-checks with `tsc -b` before
  bundling.

## Swapping in a free-tier model provider

Any provider exposing an OpenAI-compatible chat completions endpoint plugs into the same
`IChatClient` surface used above, via the `OpenAI` + `Microsoft.Extensions.AI.OpenAI` NuGet packages
(`OpenAI` 2.12.x is the version constraint `Microsoft.Extensions.AI.OpenAI` 10.9.0 expects):

```csharp
var options = new OpenAIClientOptions {
    Endpoint = new Uri("https://generativelanguage.googleapis.com/v1beta/openai/") // Gemini's OpenAI-compatible endpoint
};
var client = new OpenAIClient(new ApiKeyCredential(apiKey), options);
IChatClient chatClient = client.GetChatClient("gemini-2.5-flash").AsIChatClient();
```

This compiles cleanly against the real package surface. Gemini's free tier (2.5 Flash, roughly 15
requests/minute, 1,500/day at time of writing) is enough for a personal demo at zero cost.

## Connecting to Microsoft Foundry

Verified by actually restoring these packages and compiling the snippet in `05-agents-and-intent.md`,
not guessed:

- `Azure.AI.Projects` 2.0.1 — the Foundry project client. Exposes `AIProjectClient.ProjectOpenAIClient`,
  whose `GetChatClient(model)` returns a plain `OpenAI.Chat.ChatClient` (from the `Azure.AI.Extensions.OpenAI`
  namespace, shipped inside this package) — the same type the Azure OpenAI path in
  `05-agents-and-intent.md` produces.
- `Azure.Identity` 1.21.0 — `DefaultAzureCredential`. Passes directly as the constructor's
  `AuthenticationTokenProvider` argument; `Azure.Core.TokenCredential` implements that type in the
  current unified Azure auth model, so no separate adapter call is needed.
- `OpenAI` 2.13.0, `Microsoft.Extensions.AI.OpenAI` 10.9.0, `Microsoft.Extensions.AI` 10.9.0 — same
  bridge packages the Azure OpenAI and Gemini paths use.

Confirmed API surface:
- `AIProjectClient(Uri endpoint, AuthenticationTokenProvider credential, AIProjectClientOptions? options = null)`
- `AIProjectClient.ProjectOpenAIClient` → `Azure.AI.Extensions.OpenAI.ProjectOpenAIClient`
- `ProjectOpenAIClient.GetChatClient(string model)` → `OpenAI.Chat.ChatClient`

One caveat carried over from the Gemini path: `Microsoft.Extensions.AI.OpenAI` 10.9.0 constrains its
`OpenAI` dependency to `>= 2.12.0 && < 2.13.0`; the latest `OpenAI` package (2.13.0 at time of writing)
resolves outside that range and NuGet emits a `NU1608` warning. It's a version-constraint warning, not a
build error — the snippet still compiles and runs against 2.13.0.

`Azure.AI.Inference` and `Microsoft.Extensions.AI.AzureAIInference` — an alternative, lower-level
Foundry client — were checked too but only resolve as prerelease packages (1.0.0-beta.5 and
10.0.0-preview.1 respectively) at time of writing, so `Azure.AI.Projects` is the stable path documented
here.
