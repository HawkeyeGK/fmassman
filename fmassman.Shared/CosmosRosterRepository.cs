using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace fmassman.Shared
{
    public class CosmosRosterRepository : IRosterRepository
    {
        private readonly Container _container;
        private readonly ILogger<CosmosRosterRepository> _logger;

        public CosmosRosterRepository(CosmosClient cosmosClient, IOptions<CosmosSettings> options, ILogger<CosmosRosterRepository> logger)
        {
            var settings = options.Value;
            _container = cosmosClient.GetContainer(settings.DatabaseName, settings.PlayerContainer);
            _logger = logger;
        }

        public async Task<List<PlayerImportData>> LoadAsync()
        {
            _logger.LogInformation("Loading all players from Cosmos DB...");
            var query = _container.GetItemQueryIterator<PlayerImportData>(new QueryDefinition("SELECT * FROM c"));
            var results = new List<PlayerImportData>();
            while (query.HasMoreResults)
            {
                var response = await query.ReadNextAsync();
                results.AddRange(response.ToList());
            }
            _logger.LogInformation("Successfully loaded {Count} players.", results.Count);
            return results;
        }

        public async Task SaveAsync(List<PlayerImportData> players)
        {
            _logger.LogInformation("Starting bulk save for {Count} players.", players.Count);
            var stopwatch = Stopwatch.StartNew();
            
            // Use parallel upserts - with AllowBulkExecution = true on CosmosClient,
            // the SDK will automatically batch these concurrent tasks efficiently
            var concurrentTasks = new List<Task>();
            foreach (var player in players)
            {
                concurrentTasks.Add(_container.UpsertItemAsync(player, new PartitionKey(player.PlayerName)));
            }
            await Task.WhenAll(concurrentTasks);
            
            stopwatch.Stop();
            _logger.LogInformation("Bulk save completed in {ElapsedMilliseconds}ms.", stopwatch.ElapsedMilliseconds);
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

        public async Task UpsertAsync(PlayerImportData player)
        {
            await _container.UpsertItemAsync(player, new PartitionKey(player.PlayerName));
        }

        public async Task UpdatePlayerTagsAsync(string playerName, List<string> tagIds)
        {
            var patchOperations = new List<PatchOperation>
            {
                PatchOperation.Replace("/TagIds", tagIds)
            };

            await _container.PatchItemAsync<PlayerImportData>(
                id: playerName,
                partitionKey: new PartitionKey(playerName),
                patchOperations: patchOperations
            );
        }

        public async Task UpdatePlayerPositionAsync(string playerName, string? positionId)
        {
            var patchOperations = new List<PatchOperation>
            {
                PatchOperation.Replace("/positionId", positionId)
            };

            await _container.PatchItemAsync<PlayerImportData>(
                id: playerName,
                partitionKey: new PartitionKey(playerName),
                patchOperations: patchOperations
            );
        }

        public async Task<PlayerImportData?> GetByIdAsync(string id)
        {
            try
            {
                var response = await _container.ReadItemAsync<PlayerImportData>(id, new PartitionKey(id));
                return response.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
        }
        public Task<bool> PushToMiroAsync(string playerId)
        {
            throw new System.NotImplementedException("This method is only for client-side API calls.");
        }
    }
}
