using Azure.Monitor.OpenTelemetry.Exporter;
using FlightAi.Core.Services.Pricing;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING")))
{
    builder.Services.AddOpenTelemetry()
        .UseFunctionsWorkerDefaults()
        .UseAzureMonitorExporter();
}

// Same signing key as FlightAi.Api (task 21), held in this host's own configuration -- never source,
// never shared storage. No safe default: a booking saga that can't verify a price shouldn't start.
var signingKey = builder.Configuration["PriceAssertion:SigningKey"]
    ?? throw new InvalidOperationException("PriceAssertion:SigningKey must be configured.");
var assertionValidity = TimeSpan.FromMinutes(builder.Configuration.GetValue("PriceAssertion:ValidityMinutes", 5));
builder.Services.AddSingleton(new PriceAssertionService(signingKey, assertionValidity));

builder.Build().Run();
