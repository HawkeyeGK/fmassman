using System;
using System.Threading.Tasks;
using fmassman.Shared.Interfaces;
using fmassman.Shared.Models;
using fmassman.Shared;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;

namespace fmassman.Api.Repositories
{
    public class CosmosSettingsRepository : ISettingsRepository
    {
        private Container? _container;
        private readonly CosmosClient _cosmosClient;
        private readonly CosmosSettings _settings;
        private const string ContainerName = "settings";

        public CosmosSettingsRepository(CosmosClient cosmosClient, IOptions<CosmosSettings> settings)
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

        public async Task UpsertMiroTokensAsync(MiroTokenSet tokens)
        {
            var container = await GetContainerAsync();
            // Partition key is /id, and the id of the document is tokens.Id ("miro_tokens")
            await container.UpsertItemAsync(tokens, new PartitionKey(tokens.Id));
        }

        public async Task<MiroTokenSet?> GetMiroTokensAsync()
        {
            var container = await GetContainerAsync();
            try
            {
                var response = await container.ReadItemAsync<MiroTokenSet>("miro_tokens", new PartitionKey("miro_tokens"));
                return response.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
        }
    }
}
