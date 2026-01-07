using fmassman.Shared;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace fmassman.Api.Repositories
{
    public class CosmosTacticRepository : ITacticRepository
    {
        private Container? _container;
        private readonly CosmosClient _cosmosClient;
        private readonly CosmosSettings _settings;
        private readonly ILogger<CosmosTacticRepository> _logger;
        private const string ContainerName = "tactics";

        public CosmosTacticRepository(CosmosClient cosmosClient, IOptions<CosmosSettings> options, ILogger<CosmosTacticRepository> logger)
        {
            _cosmosClient = cosmosClient;
            _settings = options.Value;
            _logger = logger;
            // Initialization is done lazily or we can kick it off here. 
            // However, async work in constructor is bad practice. 
            // We will initialize _container ensuring it exists when we need it, 
            // or just follow the instruction to "Ensure ... is called".
            // Since we can't await in constructor, we'll do it in a helper or check in each method.
            // But the user pattern in Shared/CosmosRosterRepository.cs was:
            // _container = cosmosClient.GetContainer(settings.DatabaseName, settings.PlayerContainer);
            // which assumes it exists.
            // The user requested: "Constructor: Ensure _container.CreateContainerIfNotExistsAsync... is called (or handled lazily)"
            // I'll handle it lazily to avoid blocking constructor or async void risks.
        }

        private async Task<Container> GetContainerAsync()
        {
            if (_container != null) return _container;

            var database = _cosmosClient.GetDatabase(_settings.DatabaseName);
            await database.CreateContainerIfNotExistsAsync(ContainerName, "/id");
            _container = _cosmosClient.GetContainer(_settings.DatabaseName, ContainerName);
            return _container;
        }

        public async Task<List<Tactic>> GetAllAsync()
        {
            var container = await GetContainerAsync();
            var query = container.GetItemQueryIterator<Tactic>(new QueryDefinition("SELECT * FROM c"));
            var results = new List<Tactic>();
            while (query.HasMoreResults)
            {
                var response = await query.ReadNextAsync();
                results.AddRange(response.ToList());
            }
            return results;
        }

        public async Task SaveAsync(Tactic tactic)
        {
            var container = await GetContainerAsync();
            await container.UpsertItemAsync(tactic, new PartitionKey(tactic.Id));
        }

        public async Task DeleteAsync(string id)
        {
            var container = await GetContainerAsync();
            try
            {
                await container.DeleteItemAsync<Tactic>(id, new PartitionKey(id));
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Ignore
            }
        }
    }
}
