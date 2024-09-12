using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;

namespace BackEnd.Entities
{
    public class CosmosDbContext
    {
        public Container UsersContainer { get; }

        public CosmosDbContext(CosmosClient cosmosClient, IConfiguration configuration)
        {
            var databaseName = configuration["CosmosDbSettings:DatabaseName"];
            var usersContainerName = configuration["CosmosDbSettings:UsersContainerName"];

            UsersContainer = cosmosClient.GetContainer(databaseName, usersContainerName);
        }
    }
}
