using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace fmassman.Shared
{
    public class CosmosRosterRepository : IRosterRepository
    {
        private readonly Container _container;

        public CosmosRosterRepository(CosmosClient cosmosClient, IOptions<CosmosSettings> options)
        {
            var settings = options.Value;
            _container = cosmosClient.GetContainer(settings.DatabaseName, settings.PlayerContainer);
        }

        public async Task<List<PlayerImportData>> LoadAsync()
        {
            var query = _container.GetItemQueryIterator<PlayerImportData>(new QueryDefinition("SELECT * FROM c"));
            var results = new List<PlayerImportData>();
            while (query.HasMoreResults)
            {
                var response = await query.ReadNextAsync();
                results.AddRange(response.ToList());
            }
            return results;
        }

        public async Task SaveAsync(List<PlayerImportData> players)
        {
            // Use parallel upserts - with AllowBulkExecution = true on CosmosClient,
            // the SDK will automatically batch these concurrent tasks efficiently
            var concurrentTasks = new List<Task>();
            foreach (var player in players)
            {
                concurrentTasks.Add(_container.UpsertItemAsync(player, new PartitionKey(player.PlayerName)));
            }
            await Task.WhenAll(concurrentTasks);
        }

        public async Task DeleteAsync(string playerName)
        {
            try
            {
                await _container.DeleteItemAsync<PlayerImportData>(playerName, new PartitionKey(playerName));
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Ignore if not found
            }
        }
    }
}
