using FlightAi.Agents.Services.Intent;
using FlightAi.Api;
using FlightAi.Core.Interfaces.Suppliers;
using FlightAi.Core.Models.Suppliers;
using FlightAi.Core.Services.Suppliers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;

var builder = WebApplication.CreateBuilder(args);

// Origins come from configuration, not a compile-time list -- the deployed frontend's URL isn't known
// at build time (task 19). Empty/missing config means no cross-origin caller is allowed; same-origin
// requests (and curl) are never subject to CORS in the first place, so that's a safe default.
const string FrontendCorsPolicy = "Frontend";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy(
    FrontendCorsPolicy, policy => policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddSingleton<IChatClient>(_ => DemoOfflineChatClient.Create());
builder.Services.AddSingleton(sp => IntentAgentFactory.Create(sp.GetRequiredService<IChatClient>()));
builder.Services.AddSingleton(BuildSupplierOrchestrator());

var app = builder.Build();

// UseCors() with no name just wires the CORS middleware into the pipeline -- it doesn't apply a policy
// to anything by itself. Each endpoint opts in to a specific named policy via .RequireCors(...) below,
// which is what actually decides who gets Access-Control-Allow-Origin. With one endpoint today this
// could be UseCors(FrontendCorsPolicy) instead (a single default for everything), but per-endpoint is
// the form that doesn't need revisiting the moment a second endpoint wants a different policy -- or none.
app.UseCors();

// Single feature so far -- switch to a MapGroup + IEndpointRouteBuilder-extension-per-feature pattern
// (an Endpoints/<Feature>/ folder per area) the moment a second one shows up here.
app.MapGet("/api/search/stream", (
        [FromQuery(Name = "q")] string searchQuery, HttpContext context, IntentAgent intentAgent,
        SupplierFanOutOrchestrator orchestrator, IChatClient chatClient) =>
    Results.ServerSentEvents(SearchPipeline.RunAsync(searchQuery, intentAgent, orchestrator, chatClient, context.RequestAborted)))
    .RequireCors(FrontendCorsPolicy);

app.Run();

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
