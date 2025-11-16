
using Gateway.routes;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;

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
                ));

            var app = builder.Build();

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

