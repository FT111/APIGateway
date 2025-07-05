
using Gateway.routes;

namespace Gateway
{
    internal static class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddOpenApi();

            var app = builder.Build();

            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
            }

            // app.UseHttpsRedirection();
            
            ApiRoutes.Init(app);

            app.Run();
        }
    }
    
}

