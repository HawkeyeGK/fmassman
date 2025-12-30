using System.Net.Http.Json;
using fmassman.Shared;

namespace fmassman.Client.Services
{
    public class ApiRosterService : IRosterRepository
    {
        private readonly HttpClient _http;

        public ApiRosterService(HttpClient http)
        {
            _http = http;
        }

        public async Task<List<PlayerImportData>> LoadAsync()
        {
            return await _http.GetFromJsonAsync<List<PlayerImportData>>("api/roster") ?? new List<PlayerImportData>();
        }

        public async Task SaveAsync(List<PlayerImportData> players)
        {
            await _http.PostAsJsonAsync("api/roster", players);
        }

        public async Task DeleteAsync(string playerName)
        {
            await _http.DeleteAsync($"api/roster/{playerName}");
        }

        public async Task UpsertAsync(PlayerImportData player)
        {
            await SaveAsync(new List<PlayerImportData> { player });
        }
    }
}
