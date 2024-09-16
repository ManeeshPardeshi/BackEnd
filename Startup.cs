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
            _configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            try
            {
                var keyVaultEndpoint = new Uri("https://tenx.vault.azure.net/");

                var updatedConfiguration = new ConfigurationBuilder()
                    .AddConfiguration(_configuration)
                    .AddAzureKeyVault(keyVaultEndpoint, new DefaultAzureCredential())
                    .Build();

                var cosmosDbConnectionString = updatedConfiguration["CosmosDbConnectionString"];
                var blobConnectionString = updatedConfiguration["BlobConnectionString"];
                var serviceBusConnectionString = updatedConfiguration["ServiceBusConnectionString"];

                if (string.IsNullOrEmpty(cosmosDbConnectionString) ||
                    string.IsNullOrEmpty(blobConnectionString) ||
                    string.IsNullOrEmpty(serviceBusConnectionString))
                {
                    throw new Exception("Connection strings are missing.");
                }

                CosmosClientOptions clientOptions = new CosmosClientOptions
                {
                    ConnectionMode = ConnectionMode.Direct,
                    MaxRequestsPerTcpConnection = 10,
                    MaxTcpConnectionsPerEndpoint = 10
                };

                CosmosClient cosmosClient = new CosmosClient(cosmosDbConnectionString, clientOptions);
                services.AddSingleton(cosmosClient);
                services.AddScoped<CosmosDbContext>();

                services.AddSingleton(x => new BlobServiceClient(blobConnectionString));
                services.AddSingleton(x => new ServiceBusClient(serviceBusConnectionString));

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
            app.UseAuthorization();

            app.UseEndpoints(endpoints => endpoints.MapControllers());
        }
    }
}
