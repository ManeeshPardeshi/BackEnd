using Azure.Storage.Blobs;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using BackEnd.Entities;
using Microsoft.Azure.Cosmos;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;

namespace BackEnd
{
    public class Startup
    {
        private readonly IConfiguration _configuration;

        public Startup(IConfiguration configuration)
        {
            _configuration = configuration; // Initialize readonly field in constructor
        }

        public void ConfigureServices(IServiceCollection services)
        {
            try
            {
                var keyVaultEndpoint = new Uri("https://tenx.vault.azure.net/");

                // Fetching secrets from Azure Key Vault
                var updatedConfiguration = new ConfigurationBuilder()
                    .AddConfiguration(_configuration)
                    .AddAzureKeyVault(keyVaultEndpoint, new DefaultAzureCredential()) // Use DefaultAzureCredential for managed identity
                    .Build();

                // Retrieve secrets from Key Vault
                var cosmosDbConnectionString = updatedConfiguration["CosmosDbConnectionString"];
                var blobConnectionString = updatedConfiguration["BlobConnectionString"];
                var serviceBusConnectionString = updatedConfiguration["ServiceBusConnectionString"]; // New service bus connection string

                if (string.IsNullOrEmpty(cosmosDbConnectionString))
                    throw new Exception("CosmosDB connection string is missing or invalid.");

                if (string.IsNullOrEmpty(blobConnectionString))
                    throw new Exception("Blob connection string is missing or invalid.");

                if (string.IsNullOrEmpty(serviceBusConnectionString))
                    throw new Exception("Service Bus connection string is missing or invalid.");

                // Configure CosmosDB client
                CosmosClientOptions clientOptions = new CosmosClientOptions
                {
                    ConnectionMode = ConnectionMode.Direct,   // Direct connection mode for better performance
                    MaxRequestsPerTcpConnection = 10,         // Limit the requests per TCP connection
                    MaxTcpConnectionsPerEndpoint = 10         // Limit TCP connections per endpoint
                };

                CosmosClient cosmosClient = new CosmosClient(cosmosDbConnectionString, clientOptions);
                services.AddSingleton(cosmosClient);

                // Register CosmosDbContext
                services.AddScoped<CosmosDbContext>();

                // Register BlobServiceClient for handling Blob Storage
                services.AddSingleton(x => new BlobServiceClient(blobConnectionString));

                // Register ServiceBusClient for Azure Service Bus
                services.AddSingleton(x => new ServiceBusClient(serviceBusConnectionString));

                // Add Controllers
                services.AddControllers();

                // Add Swagger for API documentation
                services.AddSwaggerGen();
            }
            catch (Exception ex)
            {
                // Log any errors during service configuration
                Console.WriteLine($"Error in ConfigureServices: {ex.Message}");
                throw;
            }
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            try
            {
                if (env.IsDevelopment())
                {
                    app.UseDeveloperExceptionPage();
                    app.UseSwagger();
                    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1"));
                }

                app.UseHttpsRedirection();
                app.UseRouting();
                app.UseAuthorization();

                // Map Controllers to Routes
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapControllers();
                });
            }
            catch (Exception ex)
            {
                // Log any errors during app configuration
                Console.WriteLine($"Error in Configure: {ex.Message}");
                throw;
            }
        }
    }
}
