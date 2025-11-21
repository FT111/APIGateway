
using Gateway.routes;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Gateway
{
    internal class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            var builtGateway = await new GatewayBuilder(builder.Configuration)
                .BuildFromConfiguration();
            
            builder.Services.AddOpenApi();
            
            builder.Logging.ClearProviders();
            builder.Services.AddOpenTelemetry()
                .ConfigureResource(r => r.AddService(builder.Environment.ApplicationName))
                .WithLogging(logging => logging.AddConsoleExporter())
                .ConfigureResource(r => r.AddAttributes(
                    new Dictionary<string, object>
                    {
                        ["service.instance.id"] = builtGateway.Gateway.Identity.Id.ToString()
                    }
                ))
                .WithTracing(tracing => tracing
                    .AddSource("Gateway")
                    .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(builder.Environment.ApplicationName))
                    .AddZipkinExporter(z => z.Endpoint = new Uri("http://clusterhost:9411/api/v2/spans"))
                    .AddAspNetCoreInstrumentation(asp => asp.RecordException = true)
                );
                // .WithMetrics(metrics =>
                //     metrics.AddOtlpExporter(b => b.Endpoint = new Uri("http://clusterhost:9411/api/v2/metrics"))
                // );
                

            var app = builder.Build();
            builtGateway.Gateway.AddLogger(app.Logger);

            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
            }

            // app.UseHttpsRedirection();

            
            
            await new Proxy().Init(app, builtGateway.Gateway);

            await app.RunAsync();
        }
    }
    
}

