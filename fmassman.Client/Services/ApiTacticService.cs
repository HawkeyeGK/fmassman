using fmassman.Shared;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Text.Json;
using System.Linq;

namespace fmassman.Client.Services
{
    public class ApiTacticService : ITacticService
    {
        private readonly HttpClient _http;
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public ApiTacticService(HttpClient http)
        {
            _http = http;
        }

        public async Task<List<Tactic>> GetAllAsync()
        {
            return await _http.GetFromJsonAsync<List<Tactic>>("api/tactics", _jsonOptions) ?? new List<Tactic>();
        }

        public async Task SaveAsync(Tactic tactic)
        {
            await _http.PostAsJsonAsync("api/tactics", tactic);
        }

        public async Task DeleteAsync(string id)
        {
            await _http.DeleteAsync($"api/tactics/{id}");
        }

        public async Task<Tactic?> GetByIdAsync(string id)
        {
            // Since we don't have a specific GetById I'll fetch all and find locally as requested.
            // If performance becomes an issue, we can add a specific endpoint later.
            var all = await GetAllAsync();
            return all.FirstOrDefault(t => t.Id == id);
        }
    }
}
