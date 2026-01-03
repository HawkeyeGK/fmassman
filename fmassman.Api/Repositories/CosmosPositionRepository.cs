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
        private readonly CosmosClient _cosmosClient;
        private readonly CosmosSettings _settings;
        private readonly Container _container;

        public CosmosPositionRepository(CosmosClient cosmosClient, IOptions<CosmosSettings> settings)
        {
            _cosmosClient = cosmosClient;
            _settings = settings.Value;
            _container = _cosmosClient.GetContainer(_settings.DatabaseName, _settings.PositionContainer);
        }

        public async Task<List<PositionDefinition>> GetAllAsync()
        {
            var query = new QueryDefinition("SELECT * FROM c");
            var iterator = _container.GetItemQueryIterator<PositionDefinition>(query);
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
            try
            {
                var response = await _container.ReadItemAsync<PositionDefinition>(id, new PartitionKey(id));
                return response.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        public async Task UpsertAsync(PositionDefinition position)
        {
            await _container.UpsertItemAsync(position, new PartitionKey(position.Id));
        }

        public async Task DeleteAsync(string id)
        {
            await _container.DeleteItemAsync<PositionDefinition>(id, new PartitionKey(id));
        }
    }
}
