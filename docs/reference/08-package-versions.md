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

- Vite 8.2, React 19.2, TypeScript 6.0.
- Vitest 4.0 + Testing Library (`@testing-library/react` 16.3, `@testing-library/user-event` 14.6,
  `@testing-library/jest-dom` 6.9) on `jsdom` 28 — no separate test runner or browser harness. Node's
  native `fetch`/`Response` globals work inside this environment without any polyfill, which matters
  for `useBookingFlow.test.ts` (task 05): it constructs real `Response` objects for a fake `fetch`.
- `oxlint` 1.79 for linting — a Rust-based linter, not ESLint; no config beyond the default ruleset.
- Scaffolded with `npm create vite@latest -- --template react-ts` and kept dependency-free otherwise —
  no state library, no UI kit, no CSS framework, no i18n library (see `11-bilingual-ui.md` for why the
  last one specifically wasn't needed). `npm run build` type-checks with `tsc -b` before bundling.

## Swapping in a free-tier model provider

Any provider exposing an OpenAI-compatible chat completions endpoint plugs into the same
`IChatClient` surface used above, via the `OpenAI` + `Microsoft.Extensions.AI.OpenAI` NuGet packages
(`OpenAI` 2.12.x is the version constraint `Microsoft.Extensions.AI.OpenAI` 10.9.0 expects):

```csharp
var options = new OpenAIClientOptions {
    Endpoint = new Uri("https://generativelanguage.googleapis.com/v1beta/openai/") // Gemini's OpenAI-compatible endpoint
};
var client = new OpenAIClient(new ApiKeyCredential(apiKey), options);
IChatClient chatClient = client.GetChatClient("gemini-3.5-flash-lite").AsIChatClient();
```

This compiles cleanly against the real package surface. Confirmed live, not just compiled, and the
model string took three tries to land on -- worth recording exactly what happened, since it's the
clearest evidence in this whole project that "verify against current docs" isn't boilerplate caution:

1. `gemini-2.5-flash` (this doc's original string) failed with a 404: "This model
   models/gemini-2.5-flash is no longer available to new users. Please update your code to use
   models/gemini-3.6-flash."
2. `gemini-3.6-flash` worked, but its free tier turned out to cap at **20 requests/day per project**
   (confirmed via the quota-exceeded error body: `quotaId: GenerateRequestsPerDayPerProjectPerModel-FreeTier`,
   `quotaValue: 20`) -- far short of the ~1,500/day this doc originally assumed, and too tight for an
   app that spends 2 model calls per search (intent + explanation).
3. `gemini-2.5-flash-lite` also 404'd as retired, redirecting to `gemini-3.5-flash-lite`.
4. `gemini-3.5-flash-lite` is the one actually used end to end for task 17 -- no daily-quota wall hit
   in that testing.

Google's own rate-limits page (`ai.google.dev/gemini-api/docs/rate-limits`, checked the same day) no
longer publishes per-model free-tier numbers at all; it now points to each project's own AI Studio
dashboard instead. There is no way to know a given model's actual free-tier ceiling from public docs
alone anymore -- the only reliable way left is to call it and read the error. Re-verify the model
string itself against current docs before reusing this, the same way the model *choice* needs
re-checking (see `docs/deployment.md`'s free-tier limits caveat) -- both have already moved once since
this doc was first written.

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
