using System.IO;
using System.Text.Json;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace fmassman.Shared.Services
{
    public class CosmosRoleService : IRoleService
    {
        private readonly Container _container;
        private readonly string _baselineFilePath;
        private readonly ILogger<CosmosRoleService> _logger;

        public CosmosRoleService(CosmosClient cosmosClient, IOptions<CosmosSettings> options, string baselineFilePath, ILogger<CosmosRoleService> logger)
        {
            var settings = options.Value;
            _container = cosmosClient.GetContainer(settings.DatabaseName, settings.RoleContainer);
            _baselineFilePath = baselineFilePath;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            _logger.LogInformation("Initializing Role Service...");
            var roles = await LoadLocalRolesAsync();
            
            if (!roles.Any() && File.Exists(_baselineFilePath))
            {
                _logger.LogInformation("Database is empty, seeding from baseline...");
                await ResetToBaselineAsync();
                roles = await LoadLocalRolesAsync();
            }

            _logger.LogInformation("Loaded {Count} roles from source.", roles.Count);
            if (roles.Any())
            {
                RoleFitCalculator.SetCache(roles);
            }
        }

        public async Task<List<RoleDefinition>> LoadLocalRolesAsync()
        {
            var query = new QueryDefinition("SELECT * FROM c");

            var iterator = _container.GetItemQueryIterator<RoleDefinition>(query);

            var results = new List<RoleDefinition>();
            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                results.AddRange(response.ToList());
            }
            return results;
        }

        public async Task SaveRolesAsync(List<RoleDefinition> roles)
        {
            foreach (var role in roles)
            {
                await _container.UpsertItemAsync(role, new PartitionKey(role.Id));
            }
            RoleFitCalculator.SetCache(roles);
        }

        public async Task ResetToBaselineAsync()
        {
            _logger.LogInformation("Starting reset to baseline...");

            // Step 1: Clear existing data
            var existingRoles = await LoadLocalRolesAsync();
            if (existingRoles.Any())
            {
                var deleteTasks = existingRoles.Select(role =>
                    _container.DeleteItemAsync<RoleDefinition>(role.Id, new PartitionKey(role.Id)));
                await Task.WhenAll(deleteTasks);
                _logger.LogInformation("Deleted {Count} existing roles.", existingRoles.Count);
            }

            // Step 2: Load baseline from file
            if (!File.Exists(_baselineFilePath))
            {
                _logger.LogWarning("Baseline file not found at {Path}", _baselineFilePath);
                return;
            }

            var json = await File.ReadAllTextAsync(_baselineFilePath);
            var baselineRoles = JsonSerializer.Deserialize<List<RoleDefinition>>(json);

            if (baselineRoles == null || !baselineRoles.Any())
            {
                _logger.LogWarning("Baseline file was empty or could not be deserialized.");
                return;
            }

            // Step 3: Seed baseline roles
            foreach (var role in baselineRoles)
            {
                await _container.UpsertItemAsync(role, new PartitionKey(role.Id));
            }
            _logger.LogInformation("Restored {Count} baseline roles.", baselineRoles.Count);

            // Step 4: Refresh cache
            RoleFitCalculator.SetCache(baselineRoles);
        }
    }
}
