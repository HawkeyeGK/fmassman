using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using fmassman.Shared.Interfaces;
using fmassman.Shared.Models;
using System.Text.Json;
using System.Collections.Generic;

namespace fmassman.Api.Functions
{
    public class MiroBoardFunctions
    {
        private readonly ISettingsRepository _settingsRepository;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<MiroBoardFunctions> _logger;

        public MiroBoardFunctions(
            ISettingsRepository settingsRepository,
            IHttpClientFactory httpClientFactory,
            ILogger<MiroBoardFunctions> logger)
        {
            _settingsRepository = settingsRepository;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        private async Task<HttpClient> GetAuthenticatedClientAsync()
        {
            var tokens = await _settingsRepository.GetMiroTokensAsync();
            if (tokens == null || string.IsNullOrEmpty(tokens.AccessToken))
            {
                throw new UnauthorizedAccessException("Miro tokens not found. Please login via the admin dashboard.");
            }

            // Check if token is expired or about to expire (5 min buffer)
            if (tokens.ExpiresAt <= DateTime.UtcNow.AddMinutes(5))
            {
                _logger.LogInformation("Miro access token expired (or expiring soon). refreshing...");

                var clientId = Environment.GetEnvironmentVariable("MiroClientId");
                var clientSecret = Environment.GetEnvironmentVariable("MiroClientSecret");

                if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
                {
                    _logger.LogError("MiroClientId or MiroClientSecret not configured.");
                    throw new UnauthorizedAccessException("Miro configuration missing on server.");
                }

                try
                {
                    var refreshClient = _httpClientFactory.CreateClient();
                    var request = new HttpRequestMessage(HttpMethod.Post, "https://api.miro.com/v1/oauth/token");
                    
                    var kvp = new List<KeyValuePair<string, string>>
                    {
                        new KeyValuePair<string, string>("grant_type", "refresh_token"),
                        new KeyValuePair<string, string>("client_id", clientId),
                        new KeyValuePair<string, string>("client_secret", clientSecret),
                        new KeyValuePair<string, string>("refresh_token", tokens.RefreshToken)
                    };

                    request.Content = new FormUrlEncodedContent(kvp);

                    var response = await refreshClient.SendAsync(request);

                    if (!response.IsSuccessStatusCode)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        _logger.LogError("Failed to refresh Miro token: {StatusCode} {Content}", response.StatusCode, errorContent);
                        throw new UnauthorizedAccessException("Failed to refresh Miro token. Please login again.");
                    }

                    var json = await response.Content.ReadAsStringAsync();
                    var tokenResponse = JsonSerializer.Deserialize<MiroRefreshTokenResponse>(json);

                    if (tokenResponse != null && !string.IsNullOrEmpty(tokenResponse.AccessToken))
                    {
                        tokens.AccessToken = tokenResponse.AccessToken;
                        // Miro might rotate the refresh token
                        if (!string.IsNullOrEmpty(tokenResponse.RefreshToken))
                        {
                            tokens.RefreshToken = tokenResponse.RefreshToken;
                        }
                        // Update expiry
                        tokens.ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);

                        await _settingsRepository.UpsertMiroTokensAsync(tokens);
                        _logger.LogInformation("Miro token refreshed successfully.");
                    }
                    else
                    {
                        throw new UnauthorizedAccessException("Invalid response during token refresh.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error refreshing Miro token.");
                    throw new UnauthorizedAccessException("Error refreshing Miro token.", ex);
                }
            }

            var client = _httpClientFactory.CreateClient("MiroAuth");
            // Ensure V2 usage for API calls
            // Note: client.BaseAddress is likely https://api.miro.com/ from Program.cs, we will append v2/ segments
            
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokens.AccessToken);
            
            return client;
        }

        // Helper class for deserializing the refresh response (snake_case)
        private class MiroRefreshTokenResponse
        {
            [System.Text.Json.Serialization.JsonPropertyName("access_token")]
            public string AccessToken { get; set; } = string.Empty;

            [System.Text.Json.Serialization.JsonPropertyName("refresh_token")]
            public string? RefreshToken { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("expires_in")]
            public int ExpiresIn { get; set; }
            
            [System.Text.Json.Serialization.JsonPropertyName("scope")]
            public string? Scope { get; set; }
        }
    }
}
