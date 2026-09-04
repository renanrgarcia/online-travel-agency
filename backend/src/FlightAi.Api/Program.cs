using System.ClientModel;
using System.Threading.RateLimiting;
using FlightAi.Agents.Services.Intent;
using FlightAi.Api;
using FlightAi.Core.Interfaces.Suppliers;
using FlightAi.Core.Models.Suppliers;
using FlightAi.Core.Services.Pricing;
using FlightAi.Core.Services.Suppliers;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.AI;
using OpenAI;

var builder = WebApplication.CreateBuilder(args);

// Origins come from configuration, not a compile-time list -- the deployed frontend's URL isn't known
// at build time (task 19). Empty/missing config means no cross-origin caller is allowed; same-origin
// requests (and curl) are never subject to CORS in the first place, so that's a safe default.
const string FrontendCorsPolicy = "Frontend";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy(
    FrontendCorsPolicy, policy => policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

// Bounds how often the search endpoint can be called at all -- LookToBookBudget (task 07) protects
// suppliers from being over-called, but nothing protected the endpoint, the model, or App Service F1's
// own daily CPU allowance before this (task 20). Fixed window is the simplest thing that bounds the
// cost; partitioned by client IP (X-Forwarded-For first, since App Service sits behind a proxy and the
// socket's remote address would otherwise be the proxy for every caller) so one noisy client doesn't
// silently deny everyone else.
const string SearchRateLimitPolicy = "SearchRateLimit";
var permitLimit = builder.Configuration.GetValue("RateLimiting:PermitLimit", defaultValue: 10);
var windowSeconds = builder.Configuration.GetValue("RateLimiting:WindowSeconds", defaultValue: 60);
builder.Services.AddRateLimiter(options =>
{
    // Default rejection status is 503 -- E2 wants the caller to see 429 specifically, so it can tell
    // "you're going too fast" apart from "the server itself is unavailable."
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = (context, _) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();
        return ValueTask.CompletedTask;
    };

    options.AddPolicy(SearchRateLimitPolicy, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(ClientKey(httpContext), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = TimeSpan.FromSeconds(windowSeconds),
            QueueLimit = 0, // reject immediately past the limit -- E2 wants a 429 now, not a queued wait
        }));
});

// A real model is used only when an API key is configured (task 17); the offline client stays the
// default so local dev and the test suite keep running free, fast, and deterministic, with no key at
// all -- a locked decision, not an oversight. Gemini's free tier via its OpenAI-compatible endpoint is
// the one dependency in this system that isn't Azure-native (see docs/deployment.md for why).
var geminiApiKey = builder.Configuration["Gemini:ApiKey"];
builder.Services.AddSingleton<IChatClient>(_ => string.IsNullOrEmpty(geminiApiKey)
    ? DemoOfflineChatClient.Create()
    : BuildGeminiChatClient(geminiApiKey));
builder.Services.AddSingleton(sp => IntentAgentFactory.Create(sp.GetRequiredService<IChatClient>()));
builder.Services.AddSingleton(BuildSupplierOrchestrator());

// Signs each offer's price so the booking saga (a separate host, task 21) can verify it came from a
// real search rather than trusting whatever a client's booking request claims. No safe default for the
// key -- same rule task 17 applies to the model key: configuration only, never source, and the app
// should fail to start rather than silently sign with something predictable.
var signingKey = builder.Configuration["PriceAssertion:SigningKey"]
    ?? throw new InvalidOperationException("PriceAssertion:SigningKey must be configured.");
var assertionValidity = TimeSpan.FromMinutes(builder.Configuration.GetValue("PriceAssertion:ValidityMinutes", defaultValue: 5));
builder.Services.AddSingleton(new PriceAssertionService(signingKey, assertionValidity));

builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler(new ExceptionHandlerOptions
{
    ExceptionHandler = async context =>
    {
        context.Response.ContentType = "application/problem+json";
        var problemDetailsService = context.RequestServices.GetRequiredService<IProblemDetailsService>();
        await problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = { Status = StatusCodes.Status500InternalServerError },
        });
    },
});

// UseCors() with no name just wires the CORS middleware into the pipeline -- it doesn't apply a policy
// to anything by itself. Each endpoint opts in to a specific named policy via .RequireCors(...) below,
// which is what actually decides who gets Access-Control-Allow-Origin. With one endpoint today this
// could be UseCors(FrontendCorsPolicy) instead (a single default for everything), but per-endpoint is
// the form that doesn't need revisiting the moment a second endpoint wants a different policy -- or none.
app.UseCors();
app.UseRateLimiter();

// Single feature so far -- switch to a MapGroup + IEndpointRouteBuilder-extension-per-feature pattern
// (an Endpoints/<Feature>/ folder per area) the moment a second one shows up here.
app.MapGet("/api/search/stream", (
        [FromQuery(Name = "q")] string searchQuery, HttpContext context, IntentAgent intentAgent,
        SupplierFanOutOrchestrator orchestrator, IChatClient chatClient, PriceAssertionService priceAssertionService) =>
    Results.ServerSentEvents(SearchPipeline.RunAsync(
        searchQuery, intentAgent, orchestrator, chatClient, priceAssertionService, context.RequestAborted)))
    .RequireCors(FrontendCorsPolicy)
    .RequireRateLimiting(SearchRateLimitPolicy);

app.Run();

// X-Forwarded-For's leftmost entry is the original client; App Service's own proxy hop would otherwise
// make every caller resolve to the same address (the proxy's), collapsing everyone into one partition.
static string ClientKey(HttpContext context)
{
    var forwardedFor = context.Request.Headers["X-Forwarded-For"].ToString();
    return string.IsNullOrEmpty(forwardedFor)
        ? context.Connection.RemoteIpAddress?.ToString() ?? "unknown"
        : forwardedFor.Split(',')[0].Trim();
}

static IChatClient BuildGeminiChatClient(string apiKey)
{
    var options = new OpenAIClientOptions { Endpoint = new Uri("https://generativelanguage.googleapis.com/v1beta/openai/") };
    var client = new OpenAIClient(new ApiKeyCredential(apiKey), options);
    // gemini-2.5-flash (the model docs/reference/08-package-versions.md was written against) has since
    // been retired for new users -- confirmed live, not guessed: Gemini's own 404 body names the
    // replacement directly. That replacement, gemini-3.6-flash, turned out to cap the free tier at 20
    // requests/DAY per project (also confirmed live, via the quota-exceeded error body) -- too tight for
    // this app's 2-calls-per-search shape. gemini-3.5-flash-lite has no such wall in the same testing
    // and is the one actually exercised end to end for this task.
    return client.GetChatClient("gemini-3.5-flash-lite").AsIChatClient();
}

static SupplierFanOutOrchestrator BuildSupplierOrchestrator()
{
    ISupplierConnector[] connectors = [new MockGdsConnector(), new MockNdcConnector(), new MockLccConnector()];
    var policy = new SupplierPolicy(
        Timeout: TimeSpan.FromSeconds(5),
        BudgetCeiling: 100, BudgetWindow: TimeSpan.FromMinutes(1),
        BreakerFailureThreshold: 3, BreakerCooldown: TimeSpan.FromMinutes(1));
    var policies = connectors.ToDictionary(c => c.Name, _ => policy);

    return new SupplierFanOutOrchestrator(connectors, policies);
}

// Exposes the entry point to FlightAi.Tests via WebApplicationFactory<Program>.
public partial class Program;
