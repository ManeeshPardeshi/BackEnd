using Azure.Storage.Blobs;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using BackEnd.Entities;
using Microsoft.Azure.Cosmos;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using System;

namespace BackEnd
{
    public class Startup
    {
        private readonly IConfiguration _configuration;

        public Startup(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            try
            {
                // Configure Azure Key Vault integration
                var keyVaultEndpoint = new Uri(_configuration["AzureKeyVault:VaultUri"]);
                var updatedConfiguration = new ConfigurationBuilder()
                    .AddConfiguration(_configuration)
                    .AddAzureKeyVault(keyVaultEndpoint, new DefaultAzureCredential())
                    .Build();

                // Fetching secrets from Azure Key Vault
                var cosmosDbConnectionString = updatedConfiguration["CosmosDbConnectionString"];
                var blobConnectionString = updatedConfiguration["BlobConnectionString"];
                var serviceBusConnectionString = updatedConfiguration["ServiceBusConnectionString"];

                if (string.IsNullOrEmpty(cosmosDbConnectionString) ||
                    string.IsNullOrEmpty(blobConnectionString) ||
                    string.IsNullOrEmpty(serviceBusConnectionString))
                {
                    throw new Exception("Connection strings are missing.");
                }

                // Configure Cosmos DB
                CosmosClientOptions clientOptions = new CosmosClientOptions
                {
                    ConnectionMode = ConnectionMode.Direct,
                    MaxRequestsPerTcpConnection = 10,
                    MaxTcpConnectionsPerEndpoint = 10
                };
                CosmosClient cosmosClient = new CosmosClient(cosmosDbConnectionString, clientOptions);
                services.AddSingleton(cosmosClient);
                services.AddScoped<CosmosDbContext>();

                // Configure Blob Storage and Service Bus
                services.AddSingleton(x => new BlobServiceClient(blobConnectionString));
                services.AddSingleton(x => new ServiceBusClient(serviceBusConnectionString));

                // Add Ocelot for API Gateway
                services.AddOcelot();

                // Adding controllers and Swagger
                services.AddControllers();
                services.AddSwaggerGen();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ConfigureServices: {ex.Message}");
                throw;
            }
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1"));
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            // Add Ocelot middleware for API Gateway
            app.UseOcelot().Wait();

            app.UseAuthorization();

            app.UseEndpoints(endpoints => endpoints.MapControllers());
        }
    }
}
