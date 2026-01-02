using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using fmassman.Shared;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;

namespace fmassman.Api.Repositories
{
    public class CosmosTagRepository : ITagRepository
    {
        private readonly CosmosClient _cosmosClient;
        private readonly CosmosSettings _settings;
        private readonly Container _container;

        public CosmosTagRepository(CosmosClient cosmosClient, IOptions<CosmosSettings> settings)
        {
            _cosmosClient = cosmosClient;
            _settings = settings.Value;
            _container = _cosmosClient.GetContainer(_settings.DatabaseName, _settings.TagContainer);
        }

        public async Task<List<TagDefinition>> GetAllAsync()
        {
            var query = new QueryDefinition("SELECT * FROM c");
            var iterator = _container.GetItemQueryIterator<TagDefinition>(query);
            var results = new List<TagDefinition>();

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                results.AddRange(response.ToList());
            }

            return results;
        }

        public async Task SaveAsync(TagDefinition tag)
        {
            await _container.UpsertItemAsync(tag, new PartitionKey(tag.Id));
        }

        public async Task DeleteAsync(string id)
        {
            // Check for player usage
            var rosterContainer = _cosmosClient.GetContainer(_settings.DatabaseName, _settings.PlayerContainer);
            var query = new QueryDefinition("SELECT VALUE COUNT(1) FROM c JOIN t IN c.TagIds WHERE t = @id")
                .WithParameter("@id", id);
            
            var iterator = rosterContainer.GetItemQueryIterator<int>(query);
            var result = await iterator.ReadNextAsync();
            var count = result.FirstOrDefault();

            if (count > 0)
            {
                throw new InvalidOperationException("Cannot delete tag that is assigned to players");
            }

            await _container.DeleteItemAsync<TagDefinition>(id, new PartitionKey(id));
        }
    }
}
