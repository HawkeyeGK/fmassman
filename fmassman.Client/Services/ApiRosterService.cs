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

        public async Task<PlayerImportData?> UploadPlayerImage(MultipartFormDataContent content, bool isGoalkeeper)
        {
            var response = await _http.PostAsync($"api/UploadPlayerImage?gk={isGoalkeeper}", content);

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<PlayerImportData>();
            }
            
            // If it failed, throw or return null. 
            // The calling component can handle exceptions or null checks, 
            // but for now let's throw to bubble up the error text like the component did previously.
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Upload failed ({response.StatusCode}): {errorBody}");
        }
    }
}
