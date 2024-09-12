using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;

namespace BackEnd
{
    public class Program
    {
        public static void Main(string[] args)
        {
            try
            {
                CreateHostBuilder(args).Build().Run();
            }
            catch (Exception ex)
            {
                // Log and display startup errors
                Console.WriteLine($"Application startup failed: {ex.Message}");
                throw; // Re-throw the exception to crash the app
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>(); // Use Startup class
                })
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders(); // Clear default logging providers
                    logging.AddConsole();     // Add console logging for better debugging
                });
    }
}
