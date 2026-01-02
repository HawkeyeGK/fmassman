using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using fmassman.Shared;

namespace fmassman.Client.Services
{
    public class ApiTagService : ITagRepository
    {
        private readonly HttpClient _httpClient;

        public ApiTagService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<List<TagDefinition>> GetAllAsync()
        {
            var result = await _httpClient.GetFromJsonAsync<List<TagDefinition>>("api/tags");
            return result ?? new List<TagDefinition>();
        }

        public async Task SaveAsync(TagDefinition tag)
        {
            await _httpClient.PostAsJsonAsync("api/tags", tag);
        }

        public async Task DeleteAsync(string id)
        {
            var response = await _httpClient.DeleteAsync($"api/tags/{id}");
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.Conflict)
                {
                    // Read the error message if possible
                    var error = await response.Content.ReadAsStringAsync();
                    throw new InvalidOperationException(string.IsNullOrEmpty(error) ? "Cannot delete tag." : error);
                }
                response.EnsureSuccessStatusCode();
            }
        }
    }
}
