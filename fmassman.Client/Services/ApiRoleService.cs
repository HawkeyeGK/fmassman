using System.Net.Http.Json;
using fmassman.Shared;
using fmassman.Shared.Services;

namespace fmassman.Client.Services
{
    public class ApiRoleService : IRoleService
    {
        private readonly HttpClient _http;

        public ApiRoleService(HttpClient http)
        {
            _http = http;
        }

        public Task InitializeAsync()
        {
            // Initialization happens on the server/API side usually, or lazily.
            // For the client, we might not need to do anything specific here 
            // unless we want to trigger a server-side init check.
            return Task.CompletedTask;
        }

        public async Task<List<RoleDefinition>> LoadLocalRolesAsync()
        {
            var roles = await _http.GetFromJsonAsync<List<RoleDefinition>>("api/roles") ?? new List<RoleDefinition>();
            RoleFitCalculator.SetCache(roles);
            return roles;
        }

        public async Task SaveRolesAsync(List<RoleDefinition> roles)
        {
            await _http.PostAsJsonAsync("api/roles", roles);
        }

        public async Task ResetToBaselineAsync()
        {
            await _http.PostAsync("api/roles/reset", null);
        }
    }
}
