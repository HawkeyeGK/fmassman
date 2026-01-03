using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using fmassman.Shared.Models;

namespace fmassman.Client.Services
{
    public class ApiPositionService : IPositionService
    {
        private readonly HttpClient _httpClient;

        public ApiPositionService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<List<PositionDefinition>> GetAllAsync()
        {
            var result = await _httpClient.GetFromJsonAsync<List<PositionDefinition>>("api/positions");
            return result ?? new List<PositionDefinition>();
        }

        public async Task UpsertAsync(PositionDefinition position)
        {
            await _httpClient.PostAsJsonAsync("api/positions", position);
        }

        public async Task DeleteAsync(string id)
        {
            var response = await _httpClient.DeleteAsync($"api/positions/{id}");
            response.EnsureSuccessStatusCode();
        }
    }
}
