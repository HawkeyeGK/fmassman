using Microsoft.Azure.Cosmos;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace fmassman.Shared.Services
{
    public class CosmosRoleService : IRoleService
    {
        private readonly Container _container;

        public CosmosRoleService(CosmosClient cosmosClient, string databaseName, string containerName)
        {
            _container = cosmosClient.GetContainer(databaseName, containerName);
        }

        public async Task InitializeAsync()
        {
            var roles = await LoadLocalRolesAsync();
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

        public Task ResetToBaselineAsync()
        {
            // Not implemented for Cosmos DB version as it relies on file system baseline operations
            return Task.CompletedTask;
        }
    }
}
