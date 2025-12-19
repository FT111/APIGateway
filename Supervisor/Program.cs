using System.Text.Json;
using System.Text.Json.Serialization;
using GatewayPluginContract.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Supervisor.auth;
using Supervisor.routes;
using Newtonsoft.Json;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using Supervisor.services;
using Instances = Supervisor.services.Instances;

namespace Supervisor;


public static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        
        builder.Services.AddOpenApi();
        
        builder.Logging.ClearProviders();
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(builder.Environment.ApplicationName))
            .WithLogging(logging => logging.AddConsoleExporter());

        CoreServiceLoader.LoadFromConfiguration(builder);
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
            options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        });

        builder.Services.AddAuthentication(auth =>
        {
            auth.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            auth.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            auth.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        }).AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters()
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = builder.Configuration["Auth:Issuer"],
                ValidAudience = builder.Configuration["Auth:Audience"],
                IssuerSigningKey =
                    new SymmetricSecurityKey(Convert.FromBase64String(builder.Configuration["Auth:Secret"]))
            };
        });

        builder.Services.AddAuthorization();

        var app = builder.Build();
        var instanceManager = app.Services.GetRequiredService<Instances.InstanceManager>();
        var pluginConfManager = app.Services.GetRequiredService<PluginInitialisation.PluginConfigManager>();

        await instanceManager.StartAsync();
        app.MapOpenApi();

        var useLocalPackages = app.Configuration.GetValue<bool>("CoreServices:PluginPackageManager:Configuration:RegisterLocalStaticRoute");

        if (useLocalPackages)
        {
            var internalPluginPackagePath = app.Configuration["CoreServices:PluginPackageManager:Configuration:PackagedPath"]?.Split('/') ?? throw new InvalidOperationException("Plugin package path not configured.");
            var packagesPath = app.Configuration["CoreServices:PluginPackageManager:Configuration:PackagesPath"] ?? throw new InvalidOperationException("Plugin packages path not configured.");
            
            app.UseStaticFiles(
                new StaticFileOptions
                {
                    ServeUnknownFileTypes = true,
                    DefaultContentType = "application/octet-stream",
                    RequestPath = $"/{packagesPath}",
                    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(
                        Path.Combine(Directory.GetCurrentDirectory(), Path.Combine(internalPluginPackagePath))),
                    OnPrepareResponse = ctx =>
                    {
                        ctx.Context.AuthenticateAsync().Wait();
                    }
                });
            
        }

    app.UseSwaggerUI(
            settings =>
            {
                // settings.SwaggerEndpoint("openapi/v1.json", "Supervisor API V1");
            });
        
        
        app.UseAuthentication();
        app.UseAuthorization();

        

        Handler.HandleRoutes(app);
        var packageManager = app.Services.GetRequiredService<PackageManager>();
        
        app.Run();

    }
}