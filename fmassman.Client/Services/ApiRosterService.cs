using System.Net.Http.Json;
using System.Text.Json;
using fmassman.Shared;

namespace fmassman.Client.Services
{
    public class ApiRosterService : IRosterRepository
    {
        private readonly HttpClient _http;
        
        // JSON options for consistent serialization/deserialization:
        // - PropertyNameCaseInsensitive: true = accept camelCase OR PascalCase on read
        // - PropertyNamingPolicy: null = serialize with PascalCase (C# property names)
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = null // Use property names as-is (PascalCase)
        };

        public ApiRosterService(HttpClient http)
        {
            _http = http;
        }

        public async Task<List<PlayerImportData>> LoadAsync()
        {
            return await _http.GetFromJsonAsync<List<PlayerImportData>>("api/roster", _jsonOptions) ?? new List<PlayerImportData>();
        }

        public async Task SaveAsync(List<PlayerImportData> players)
        {
            var response = await _http.PostAsJsonAsync("api/roster", players, _jsonOptions);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Save failed ({response.StatusCode}): {errorBody}");
            }
        }

        public async Task DeleteAsync(string playerName)
        {
            await _http.DeleteAsync($"api/roster/{playerName}");
        }

        public async Task UpsertAsync(PlayerImportData player)
        {
            await SaveAsync(new List<PlayerImportData> { player });
        }

        public async Task<PlayerImportData?> UploadPlayerImage(HttpContent content, bool isGoalkeeper)
        {
            var response = await _http.PostAsync($"api/UploadPlayerImage?gk={isGoalkeeper}", content);

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<PlayerImportData>(_jsonOptions);
            }
            
            // If it failed, throw or return null. 
            // The calling component can handle exceptions or null checks, 
            // but for now let's throw to bubble up the error text like the component did previously.
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Upload failed ({response.StatusCode}): {errorBody}");
        }

        public async Task UpdatePlayerTagsAsync(string playerName, List<string> tagIds)
        {
            var response = await _http.PatchAsJsonAsync($"api/roster/{Uri.EscapeDataString(playerName)}/tags", tagIds, _jsonOptions);

            if (!response.IsSuccessStatusCode)
            {
                 var errorBody = await response.Content.ReadAsStringAsync();
                 throw new HttpRequestException($"Update tags failed ({response.StatusCode}): {errorBody}");
            }
        }


        public async Task UpdatePlayerPositionAsync(string playerName, string? positionId)
        {
            var response = await _http.PutAsJsonAsync($"api/roster/{Uri.EscapeDataString(playerName)}/position", positionId, _jsonOptions);

            if (!response.IsSuccessStatusCode)
            {
                 var errorBody = await response.Content.ReadAsStringAsync();
                 throw new HttpRequestException($"Update position failed ({response.StatusCode}): {errorBody}");
            }
            if (!response.IsSuccessStatusCode)
            {
                 var errorBody = await response.Content.ReadAsStringAsync();
                 throw new HttpRequestException($"Update position failed ({response.StatusCode}): {errorBody}");
            }
        }

        public async Task<PlayerImportData?> GetByIdAsync(string id)
        {
             try
             {
                 return await _http.GetFromJsonAsync<PlayerImportData>($"api/roster/{Uri.EscapeDataString(id)}", _jsonOptions);
             }
             catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
             {
                 return null;
             }
        }
    }
}

