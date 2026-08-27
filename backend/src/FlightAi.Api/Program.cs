using FlightAi.Api;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/api/search/stream", (HttpContext context) =>
    Results.ServerSentEvents(SkeletonEventSource.GenerateAsync(context.RequestAborted)));

app.Run();

// Exposes the entry point to FlightAi.Tests via WebApplicationFactory<Program>.
public partial class Program;
