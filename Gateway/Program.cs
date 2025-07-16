
using Gateway.routes;

namespace Gateway
{
    internal static class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddOpenApi();

            var app = builder.Build();

            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
            }

            // app.UseHttpsRedirection();
            
            await Proxy.Init(app);

            app.Run();
        }
    }
    
}

