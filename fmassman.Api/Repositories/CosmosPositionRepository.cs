using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using fmassman.Shared;
using fmassman.Shared.Interfaces;
using fmassman.Shared.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;

namespace fmassman.Api.Repositories
{
    public class CosmosPositionRepository : IPositionRepository
    {
        private Container? _container;
        private readonly CosmosClient _cosmosClient;
        private readonly CosmosSettings _settings;
        private const string ContainerName = "positions";

        public CosmosPositionRepository(CosmosClient cosmosClient, IOptions<CosmosSettings> settings)
        {
            _cosmosClient = cosmosClient;
            _settings = settings.Value;
        }

        private async Task<Container> GetContainerAsync()
        {
            if (_container != null) return _container;

            var database = _cosmosClient.GetDatabase(_settings.DatabaseName);
            await database.CreateContainerIfNotExistsAsync(ContainerName, "/id");
            _container = _cosmosClient.GetContainer(_settings.DatabaseName, ContainerName);
            return _container;
        }

        public async Task<List<PositionDefinition>> GetAllAsync()
        {
            var container = await GetContainerAsync();
            var query = new QueryDefinition("SELECT * FROM c");
            var iterator = container.GetItemQueryIterator<PositionDefinition>(query);
            var results = new List<PositionDefinition>();

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                results.AddRange(response.ToList());
            }

            return results;
        }

        public async Task<PositionDefinition?> GetByIdAsync(string id)
        {
            var container = await GetContainerAsync();
            try
            {
                var response = await container.ReadItemAsync<PositionDefinition>(id, new PartitionKey(id));
                return response.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        public async Task UpsertAsync(PositionDefinition position)
        {
            var container = await GetContainerAsync();
            await container.UpsertItemAsync(position, new PartitionKey(position.Id));
        }

        public async Task DeleteAsync(string id)
        {
            var container = await GetContainerAsync();
            await container.DeleteItemAsync<PositionDefinition>(id, new PartitionKey(id));
        }
    }
}
