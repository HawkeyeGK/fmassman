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
using fmassman.Shared;
using System.Text.Json;
using System.Collections.Generic;

namespace fmassman.Api.Functions
{
    public class MiroBoardFunctions
    {
        private readonly ISettingsRepository _settingsRepository;
        private readonly IRosterRepository _rosterRepository;
        private readonly IPositionRepository _positionRepository;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<MiroBoardFunctions> _logger;

        public MiroBoardFunctions(
            ISettingsRepository settingsRepository,
            IRosterRepository rosterRepository,
            IPositionRepository positionRepository,
            IHttpClientFactory httpClientFactory,
            ILogger<MiroBoardFunctions> logger)
        {
            _settingsRepository = settingsRepository;
            _rosterRepository = rosterRepository;
            _positionRepository = positionRepository;
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
            
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokens.AccessToken);
            
            return client;
        }

        [Function("MiroPushPlayer")]
        public async Task<IActionResult> PushPlayerToMiro(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "miro/push/{playerId}")] HttpRequestData req,
            string playerId)
        {
            try
            {
                var player = await _rosterRepository.GetByIdAsync(playerId);
                if (player == null)
                {
                    return new NotFoundObjectResult(new { error = "Player not found" });
                }

                var client = await GetAuthenticatedClientAsync();
                var boardId = Environment.GetEnvironmentVariable("MiroBoardId");

                if (string.IsNullOrEmpty(boardId))
                {
                    _logger.LogError("MiroBoardId environment variable not set.");
                    return new ObjectResult(new { error = "Server configuration error" }) { StatusCode = 500 };
                }

                // 1. Setup & Defaults
                double posX = 0;
                double posY = 0;
                string color = "#E0E0E0";
                string roleName = "Unknown";

                if (!string.IsNullOrEmpty(player.PositionId))
                {
                    var position = await _positionRepository.GetByIdAsync(player.PositionId);
                    if (position != null)
                    {
                        color = position.ColorHex;
                        roleName = position.Name;
                    }
                }

                // 2. Smart Delete (The "Kill")
                if (!string.IsNullOrEmpty(player.MiroWidgetId))
                {
                    try
                    {
                        var getResponse = await client.GetAsync($"v2/boards/{boardId}/items/{player.MiroWidgetId}");
                        if (getResponse.IsSuccessStatusCode)
                        {
                            var itemJson = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
                            if (itemJson.TryGetProperty("position", out var posElement))
                            {
                                if (posElement.TryGetProperty("x", out var xVal)) posX = xVal.GetDouble();
                                if (posElement.TryGetProperty("y", out var yVal)) posY = yVal.GetDouble();
                            }
                            
                            // Delete the old item/group
                            await client.DeleteAsync($"v2/boards/{boardId}/items/{player.MiroWidgetId}");
                        }
                        else if (getResponse.StatusCode == HttpStatusCode.NotFound)
                        {
                             _logger.LogWarning("Miro widget {WidgetId} not found (deleted externally). Using default position.", player.MiroWidgetId);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error during Smart Delete of widget {WidgetId}", player.MiroWidgetId);
                        // Proceed with default pos if delete fails/errors? Or abort? 
                        // We'll proceed to ensure the new card is created.
                    }
                }

                JsonSerializerOptions jsonOptions = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                };

                // 3. Composition (The "Fill")
                // A. Background (Shape)
                var bgPayload = new
                {
                    data = new { shape = "rectangle" }, // No content in BG
                    style = new { fillColor = color, borderOpacity = 0 },
                    geometry = new { width = 300, height = 240 },
                    position = new { x = posX, y = posY }
                };

                var bgResponse = await client.PostAsJsonAsync($"v2/boards/{boardId}/shapes", bgPayload, jsonOptions);
                if (!bgResponse.IsSuccessStatusCode)
                {
                     var err = await bgResponse.Content.ReadAsStringAsync();
                     return new BadRequestObjectResult(new { error = "Failed to create background", details = err });
                }
                var bgJson = await bgResponse.Content.ReadFromJsonAsync<JsonElement>();
                string bgId = bgJson.GetProperty("id").GetString();

                // B. Header (Text) - Name
                // Position: Top of card (approx y - 80)
                var headerPayload = new
                {
                    data = new { content = $"<h3>{player.PlayerName}</h3>" },
                    style = new { textAlign = "center", fontSize = 20 },
                    position = new { x = posX, y = posY - 80 }
                };
                var headerResponse = await client.PostAsJsonAsync($"v2/boards/{boardId}/texts", headerPayload, jsonOptions);
                var headerJson = await headerResponse.Content.ReadFromJsonAsync<JsonElement>();
                string headerId = headerJson.GetProperty("id").GetString();

                // C. Body (Text) - Stats
                // Position: Middle/Bottom (approx y + 20)
                var age = player.Snapshot?.Age.ToString() ?? "?";
                var contract = player.Snapshot?.ContractExpiry ?? "?";
                var bodyString = $"<p><strong>Role:</strong> {roleName}</p>" +
                                 $"<p><strong>Age:</strong> {age}</p>" +
                                 $"<p><strong>Contract:</strong> {contract}</p>";

                var bodyPayload = new
                {
                    data = new { content = bodyString },
                    style = new { textAlign = "center", fontSize = 14 },
                    position = new { x = posX, y = posY + 20 }
                };
                var bodyResponse = await client.PostAsJsonAsync($"v2/boards/{boardId}/texts", bodyPayload, jsonOptions);
                var bodyJson = await bodyResponse.Content.ReadFromJsonAsync<JsonElement>();
                string bodyId = bodyJson.GetProperty("id").GetString();

                // 4. Grouping
                // V2 API Endpoint for Grouping: POST https://api.miro.com/v2/boards/{board_id}/groups
                var groupPayload = new
                {
                    data = new { items = new[] { bgId, headerId, bodyId } }
                };

                var groupResponse = await client.PostAsJsonAsync($"v2/boards/{boardId}/groups", groupPayload, jsonOptions);
                
                if (!groupResponse.IsSuccessStatusCode)
                {
                    var groupError = await groupResponse.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to group items: {Status} {Error}", groupResponse.StatusCode, groupError);
                    
                    // Cleanup: Delete the loose items so we don't leave mess/duplicates
                    try 
                    {
                        var tasks = new List<Task>
                        {
                            client.DeleteAsync($"v2/boards/{boardId}/shapes/{bgId}"),
                            client.DeleteAsync($"v2/boards/{boardId}/texts/{headerId}"),
                            client.DeleteAsync($"v2/boards/{boardId}/texts/{bodyId}")
                        };
                        await Task.WhenAll(tasks);
                    }
                    catch (Exception cleanupEx)
                    {
                        _logger.LogError(cleanupEx, "Failed to clean up loose items after grouping failure.");
                    }

                    return new ObjectResult(new { error = "Created items but failed to group them. Cleaned up items.", details = groupError }) { StatusCode = 500 };
                }
                
                var groupJson = await groupResponse.Content.ReadFromJsonAsync<JsonElement>();
                if (groupJson.TryGetProperty("id", out var gId))
                {
                    player.MiroWidgetId = gId.GetString();
                    await _rosterRepository.UpsertAsync(player);
                }
                else
                {
                     // If we got success but no ID (weird), assume failure?
                     _logger.LogError("Grouping succeeded but returned no ID.");
                     return new ObjectResult(new { error = "Grouping succeeded but returned no ID." }) { StatusCode = 500 };
                }

                return new OkObjectResult(new { status = "success", widgetId = player.MiroWidgetId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error pushing player to Miro");
                return new ObjectResult(new { error = ex.Message }) { StatusCode = 500 };
            }
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
