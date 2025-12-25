using Microsoft.Azure.Cosmos;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace fmassman.Shared
{
    public class CosmosRosterRepository : IRosterRepository
    {
        private readonly Container _container;

        public CosmosRosterRepository(CosmosClient cosmosClient, string databaseName, string containerName)
        {
            _container = cosmosClient.GetContainer(databaseName, containerName);
        }

        public async Task<List<PlayerImportData>> LoadAsync()
        {
            try
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
            catch
            {
                // If the container/DB doesn't exist or connection fails, return empty list
                // This prevents the UI from crashing
                return new List<PlayerImportData>();
            }
        }

        public async Task SaveAsync(List<PlayerImportData> players)
        {
            foreach (var player in players)
            {
                // Ensure id is set if it's null (though it maps to PlayerName)
                await _container.UpsertItemAsync(player, new PartitionKey(player.PlayerName));
            }
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
