using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Supervisor.auth;
using Supervisor.routes;
using Newtonsoft.Json;
using Instances = Supervisor.services.Instances;

namespace Supervisor;


public static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddOpenApi();
        
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
                IssuerSigningKey = new SymmetricSecurityKey(Convert.FromBase64String(builder.Configuration["Auth:Secret"]))
            };
        });

        builder.Services.AddAuthorization();
        
        var app = builder.Build();
        var instanceManager = app.Services.GetRequiredService<Instances.InstanceManager>();
        await instanceManager.StartAsync();
        app.MapOpenApi();
        app.UseSwaggerUI(
            settings =>
            {
                // settings.SwaggerEndpoint("openapi/v1.json", "Supervisor API V1");
            });
        
        
        app.UseAuthentication();
        app.UseAuthorization();

        Console.WriteLine(app.Services.GetRequiredService<AuthHandler>().GeneratePasswordHash("test"));

        Handler.HandleRoutes(app);
        
        app.Run();

    }
}