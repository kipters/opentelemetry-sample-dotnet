using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NoiseMaker.Services;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Enrichers.Span;
using Serilog.Formatting.Compact;
using Serilog.Sinks.Grafana.Loki;
using Serilog.Sinks.Grafana.Loki.HttpClients;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    Log.Information("Starting app");
    BuildApp(args);
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

static void BuildApp(string[] args)
{
    Host.CreateDefaultBuilder(args)
        .UseSerilog((context, config) =>
        {
            config
                .MinimumLevel.Information()
                .Enrich.FromLogContext()
                .Enrich.WithSpan()
                .WriteTo.Console()
                .WriteTo.GrafanaLoki(
                    "http://localhost:3100"
                    , httpClient: new LokiClient()
                    , labels: new[] { new LokiLabel { Key = "service", Value = "NoiseMaker" } }
                    // , propertiesAsLabels: new[] { "SourceContext" }
                );
        })
        .ConfigureServices((context, services) =>
        {
            services.AddOpenTelemetry()
                .WithMetrics(_ => _
                    .AddRuntimeInstrumentation()
                    .AddMeter("SystemInfo")
                    .AddMeter("TickerService")
                    .AddOtlpExporter(e =>
                    {
                        e.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
                    })
                )
                .WithTracing(_ => _
                    .AddSource("TickerService")
                    .AddOtlpExporter()
                )
                .ConfigureResource(r => r
                    .AddTelemetrySdk()
                    .AddAttributes(new KeyValuePair<string, object>[] 
                    { 
                        new("deployment.environment", context.HostingEnvironment.EnvironmentName),
                        new("service", "NoiseMaker")
                    })
                    .AddService("NoiseMaker", autoGenerateServiceInstanceId: true)
                );

            services.AddHostedService<TickerService>();
            services.AddHostedService<DiskSpaceService>();
        })
        .Build()
        .Run();
}

public class LokiClient : LokiHttpClient
{
    public override async Task<HttpResponseMessage> PostAsync(string requestUri, Stream contentStream)
    {
        var resp = await base.PostAsync(requestUri, contentStream);
        return resp;
    }
}