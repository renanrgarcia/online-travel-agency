using FlightAi.Agents.Services.Intent;
using FlightAi.Api;
using FlightAi.Core.Interfaces.Suppliers;
using FlightAi.Core.Models.Suppliers;
using FlightAi.Core.Services.Suppliers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IChatClient>(_ => DemoOfflineChatClient.Create());
builder.Services.AddSingleton(sp => IntentAgentFactory.Create(sp.GetRequiredService<IChatClient>()));
builder.Services.AddSingleton(BuildSupplierOrchestrator());

var app = builder.Build();

// Single feature so far -- switch to a MapGroup + IEndpointRouteBuilder-extension-per-feature pattern
// (an Endpoints/<Feature>/ folder per area) the moment a second one shows up here.
app.MapGet("/api/search/stream", (
        [FromQuery(Name = "q")] string searchQuery, HttpContext context, IntentAgent intentAgent,
        SupplierFanOutOrchestrator orchestrator, IChatClient chatClient) =>
    Results.ServerSentEvents(SearchPipeline.RunAsync(searchQuery, intentAgent, orchestrator, chatClient, context.RequestAborted)));

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
