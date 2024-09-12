﻿using Microsoft.Azure.Cosmos;
using Azure.Identity;
using BackEnd.Entities;
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
                // Azure Key Vault Integration
                var keyVaultEndpoint = new Uri("https://tenx.vault.azure.net/");

                // Create a local variable for updated configuration
                var updatedConfiguration = new ConfigurationBuilder()
                    .AddConfiguration(_configuration)
                    .AddAzureKeyVault(keyVaultEndpoint, new DefaultAzureCredential())
                    .Build();

                // Retrieve CosmosDB connection string from the updated configuration
                var cosmosDbConnectionString = updatedConfiguration["CosmosDbConnectionString"];

                if (string.IsNullOrEmpty(cosmosDbConnectionString))
                {
                    throw new Exception("CosmosDB connection string is missing or invalid.");
                }

                // Configure CosmosDB client with connection pooling optimization
                CosmosClientOptions clientOptions = new CosmosClientOptions
                {
                    ConnectionMode = ConnectionMode.Direct,   // Direct connection mode for better performance
                    MaxRequestsPerTcpConnection = 10,         // Limit the requests per TCP connection
                    MaxTcpConnectionsPerEndpoint = 10         // Limit TCP connections per endpoint
                };

                CosmosClient cosmosClient = new CosmosClient(cosmosDbConnectionString, clientOptions);

                // Register CosmosClient as Singleton
                services.AddSingleton(cosmosClient);

                // Register the custom CosmosDbContext
                services.AddScoped<CosmosDbContext>();

                // Add Controller services
                services.AddControllers();

                // Add Swagger for API documentation
                services.AddSwaggerGen();
            }
            catch (Exception ex)
            {
                // Log any errors that occur during service configuration
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
                // Log any errors during the app configuration
                Console.WriteLine($"Error in Configure: {ex.Message}");
                throw;
            }
        }
    }
}
