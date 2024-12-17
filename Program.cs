using System.Diagnostics;
using System.Diagnostics.Metrics;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);
// Custom metrics for the application
var greeterMeter = new Meter("servico-teste", "1.0.0");
var countGreetings = greeterMeter.CreateCounter<int>("greetings.count", description: "Counts the number of greetings");
// Custom ActivitySource for the application
var greeterActivitySource = new ActivitySource("servico-teste");

// Setup logging to be exported via OpenTelemetry
builder.Logging.AddOpenTelemetry(logging =>
{
    logging.ParseStateValues = true;
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;
    logging.AddOtlpExporter(opt => opt.Endpoint = new Uri("http://localhost:4317"));
});

// Setup telemetry to be exported by OpenTelemetry
var otel = builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource =>
    {
        resource.AddService("servico-teste", serviceVersion: "1.0.0", serviceInstanceId: "api");
    })
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddOtlpExporter(opt => opt.Endpoint = new Uri("http://localhost:4317"));
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddMeter(greeterMeter.Name)
            .AddMeter("Microsoft.AspNetCore.Hosting")
            .AddMeter("Microsoft.AspNetCore.Server.Kestrel")
            .AddOtlpExporter(opt => opt.Endpoint = new Uri("http://localhost:4317"));
    })
    ;

var app = builder.Build();
app.MapGet("/", SendGreeting);

async Task<String> SendGreeting(ILogger<Program> logger)
{
    // Create a new Activity scoped to the method
    using var activity = greeterActivitySource.StartActivity("GreeterActivity");

    // Log a message
    logger.LogInformation("Sending greeting");

    // Increment the custom counter
    countGreetings.Add(1);

    // Add a tag to the Activity
    activity?.SetTag("greeting", "Hello World!");

    return "Hello World!";
}

app.Run();
