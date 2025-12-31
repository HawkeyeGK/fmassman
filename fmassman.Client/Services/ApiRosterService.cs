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

        // DEBUG: Expose last save response info
        public string? LastSaveError { get; private set; }
        public int? LastSaveStatusCode { get; private set; }
        
        public async Task SaveAsync(List<PlayerImportData> players)
        {
            LastSaveError = null;
            LastSaveStatusCode = null;
            
            var response = await _http.PostAsJsonAsync("api/roster", players, _jsonOptions);
            LastSaveStatusCode = (int)response.StatusCode;
            
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                LastSaveError = $"Save failed ({response.StatusCode}): {errorBody}";
                throw new HttpRequestException(LastSaveError);
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
    }
}

