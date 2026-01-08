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
        private readonly fmassman.Shared.Services.IRoleService _roleService; // Inject RoleService
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly PlayerAnalyzer _playerAnalyzer;
        private readonly ILogger<MiroBoardFunctions> _logger;

        public MiroBoardFunctions(
            ISettingsRepository settingsRepository,
            IRosterRepository rosterRepository,
            IPositionRepository positionRepository,
            fmassman.Shared.Services.IRoleService roleService,
            IHttpClientFactory httpClientFactory,
            PlayerAnalyzer playerAnalyzer,
            ILogger<MiroBoardFunctions> logger)
        {
            _settingsRepository = settingsRepository;
            _rosterRepository = rosterRepository;
            _positionRepository = positionRepository;
            _roleService = roleService;
            _httpClientFactory = httpClientFactory;
            _playerAnalyzer = playerAnalyzer;
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
                // CRITICAL: Initialize Roles so Calculator works
                await _roleService.InitializeAsync();

                var player = await _rosterRepository.GetByIdAsync(playerId);
                if (player == null)
                {
                    return new NotFoundObjectResult(new { error = "Player not found" });
                }

                _logger.LogInformation("Retrieved player {PlayerName} with MiroWidgetId: {MiroWidgetId}", 
                    player.PlayerName, 
                    player.MiroWidgetId ?? "(null)");

                var client = await GetAuthenticatedClientAsync();
                var boardId = Environment.GetEnvironmentVariable("MiroBoardId");

                if (string.IsNullOrEmpty(boardId))
                {
                    _logger.LogError("MiroBoardId environment variable not set.");
                    return new ObjectResult(new { error = "Server configuration error" }) { StatusCode = 500 };
                }

                // 0. Prepare Data & Helpers
                // BUGFIX: Check for missing analysis
                if (player.Analysis == null || (player.Analysis.InPossessionFits.Count == 0 && player.Analysis.OutPossessionFits.Count == 0))
                {
                    _logger.LogInformation("Analysis missing for player {PlayerName}. Calculating now...", player.PlayerName);
                    player.Analysis = _playerAnalyzer.Analyze(player.Snapshot);
                    
                    // Critical: Save it
                    await _rosterRepository.UpsertAsync(player);
                }

                var analysis = player.Analysis!; // Should be safe now

                double posX = 0;
                double posY = 0;
                string color = "#E0E0E0";
                string roleName = "Unknown";
                string positionCode = "";

                if (!string.IsNullOrEmpty(player.PositionId))
                {
                    var position = await _positionRepository.GetByIdAsync(player.PositionId);
                    if (position != null)
                    {
                        color = position.ColorHex;
                        roleName = position.Name;
                        positionCode = position.Code;
                    }
                }

                var deleteDetails = "Not attempted (no existing MiroWidgetId)";
                var oldWidgetId = player.MiroWidgetId;

                // STEP 1: SMART DELETE (Keep existing logic)
                if (!string.IsNullOrEmpty(player.MiroWidgetId))
                {
                    var idsToDelete = player.MiroWidgetId.Split('|').Where(x => !string.IsNullOrEmpty(x)).ToArray();
                    
                    try
                    {
                        // PHASE 1: FIND POSITION
                        var positionFound = false;
                        foreach (var id in idsToDelete)
                        {
                            deleteDetails = $"Checking for position on {id}...";
                            var getResponse = await client.GetAsync($"v2/boards/{boardId}/items/{id}");
                            
                            if (getResponse.IsSuccessStatusCode)
                            {
                                var itemJson = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
                                if (itemJson.TryGetProperty("position", out var posElement))
                                {
                                    if (posElement.TryGetProperty("x", out var xVal)) posX = xVal.GetDouble();
                                    if (posElement.TryGetProperty("y", out var yVal)) posY = yVal.GetDouble();
                                    positionFound = true;
                                    deleteDetails = $"Position found at ({posX}, {posY}) from {id}";
                                    break; 
                                }
                            }
                        }

                        if (!positionFound)
                        {
                            deleteDetails += " - Not found, defaulting to 0,0";
                        }

                        // PHASE 2: DELETE
                        foreach (var id in idsToDelete)
                        {
                            await client.DeleteAsync($"v2/boards/{boardId}/items/{id}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error during Smart Delete");
                        deleteDetails = $"Exception: {ex.Message}";
                    }
                }

                JsonSerializerOptions jsonOptions = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                };

                // STEP 2: BULK CREATE (The Composition)
                // Dimensions: 420x300.
                // Offsets: Header Y: -120. Body Y: +10. Center is (posX, posY).
                
                // 1. Background (Rectangle)
                var bgPayload = new
                {
                    data = new { shape = "rectangle" },
                    style = new { fillColor = color, borderOpacity = 0 },
                    geometry = new { width = 460, height = 300 },
                    position = new { x = posX, y = posY }
                };
                var bgResp = await PostMiroItem(client, boardId, "shapes", bgPayload, jsonOptions);
                string bgId = bgResp.Id;

                // 2. Header Name (Text) - Left
                // x: -50, y: -120. Width 340. Align Left.
                // Content: <b>Name</b>
                var headerNamePayload = new
                {
                    data = new { content = $"<b>{player.PlayerName}</b>" },
                    style = new { textAlign = "left", fontSize = 36 },
                    geometry = new { width = 340 },
                    position = new { x = posX - 50, y = posY - 120 }
                };
                var nameResp = await PostMiroItem(client, boardId, "texts", headerNamePayload, jsonOptions);
                string nameId = nameResp.Id;

                // 3. Header Code (Text) - Right
                // x: +175, y: -120. Width 90. Align Right.
                var headerCodePayload = new
                {
                    data = new { content = $"<b>{positionCode}</b>" }, // Bolding code for visibility
                    style = new { textAlign = "right", fontSize = 36 },
                    geometry = new { width = 90 },
                    position = new { x = posX + 175, y = posY - 120 }
                };
                var codeResp = await PostMiroItem(client, boardId, "texts", headerCodePayload, jsonOptions);
                string codeId = codeResp.Id;

                // 4. Body Roles (Text) - Left
                // x: -50, y: -20 (Moved Down for spacing). Width 340. Align Left.
                // Content: 2 In Possession + 2 Out Possession lines
                var rolesHtml = GetTopRoles(analysis, true) + GetTopRoles(analysis, false);
                var bodyRolesPayload = new
                {
                    data = new { content = rolesHtml },
                    style = new { textAlign = "left", fontSize = 18 }, // Keeping smaller for list
                    geometry = new { width = 340 },
                    position = new { x = posX - 50, y = posY - 20 }
                };
                var rolesResp = await PostMiroItem(client, boardId, "texts", bodyRolesPayload, jsonOptions);
                string rolesId = rolesResp.Id;

                // 5. Body Bio (Text) - Right
                // x: +175, y: -20 (Moved Down for spacing). Width 90. Align Right.
                var bioHtml = BuildBioHtml(player);
                var bodyBioPayload = new
                {
                    data = new { content = bioHtml },
                    style = new { textAlign = "right", fontSize = 18 },
                    geometry = new { width = 90 },
                    position = new { x = posX + 175, y = posY - 20 }
                };
                var bioResp = await PostMiroItem(client, boardId, "texts", bodyBioPayload, jsonOptions);
                string bioId = bioResp.Id;

                // STEP 3: GROUPING
                string groupId = "";
                var groupPayload = new
                {
                    data = new { items = new[] { bgId, nameId, codeId, rolesId, bioId } }
                };
                
                var groupResponse = await client.PostAsJsonAsync($"v2/boards/{boardId}/groups", groupPayload, jsonOptions);
                if (groupResponse.IsSuccessStatusCode)
                {
                    var gJson = await groupResponse.Content.ReadFromJsonAsync<JsonElement>();
                    if (gJson.TryGetProperty("id", out var gIdProp))
                    {
                        groupId = gIdProp.GetString()!;
                    }
                }

                // Save Compound ID
                var compoundId = $"{groupId}|{bgId}|{nameId}|{codeId}|{rolesId}|{bioId}";
                player.MiroWidgetId = compoundId;
                await _rosterRepository.UpsertAsync(player);

                return new OkObjectResult(new { status = "success", widgetId = compoundId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error pushing player to Miro");
                return new ObjectResult(new { error = ex.Message }) { StatusCode = 500 };
            }
        }

        private async Task<(string Id, bool Success)> PostMiroItem(HttpClient client, string boardId, string type, object payload, JsonSerializerOptions options)
        {
            var response = await client.PostAsJsonAsync($"v2/boards/{boardId}/{type}", payload, options);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                return (json.GetProperty("id").GetString()!, true);
            }
            return (string.Empty, false);
        }

        private string FormatCurrencyK(int? value)
        {
            if (!value.HasValue || value.Value == 0) return "-";
            var k = Math.Ceiling(value.Value / 1000.0);
            return $"${k}k";
        }

        private string GetTopRoles(PlayerAnalysis analysis, bool inPossession)
        {
            var fits = inPossession ? analysis.InPossessionFits : analysis.OutPossessionFits;
            if (fits == null || !fits.Any()) return string.Empty;

            var top = fits.OrderByDescending(f => f.Score).Take(2);
            var result = "";
            var color = inPossession ? "blue" : "red"; // Subtle visual distinction if needed, or just plain
            // Spec says "Role Name - 99%"
            foreach (var r in top)
            {
                // Simple HTML lines
                result += $"<p>{r.RoleName} - {r.Score}%</p>";
            }
            return result;
        }

        private string BuildBioHtml(PlayerImportData p)
        {
            // Line 1: Wage (FormatCurrencyK)
            // Line 2: Contract Expiration (Year only)
            // Line 3: Transfer Value High (FormatCurrencyK)
            // Line 4: Age
            
            // Wage Logic
            int? wageVal = null;
            if (int.TryParse(p.Snapshot?.Wage?.Replace(",", "")?.Replace("£", "")?.Replace("$", "")?.Replace("p/w", "")?.Trim(), out int w))
            {
                wageVal = w;
            }
            var wageStr = FormatCurrencyK(wageVal); 

            // Contract Logic
            var contractStr = p.Snapshot?.ContractExpiry ?? "-";
            if (DateTime.TryParse(contractStr, out var d))
            {
                contractStr = d.Year.ToString();
            }
            else if (contractStr.Length >= 4 && int.TryParse(contractStr.Substring(contractStr.Length - 4), out int y))
            {
                 // Handle cases like "30/06/2026" manually if Date Parse fails? 
                 // Assuming standard date format or Year. 
                 // If parse fails, just keep as is, but maybe try to grab last 4 chars?
            }

            var transferVal = FormatCurrencyK(p.Snapshot?.TransferValueHigh);
            var age = p.Snapshot?.Age.ToString() ?? "-";

            // Using simple paragraphs. Miro Text item handles alignment if we set textAlign: right in the payload.
            return $"<p>{wageStr}</p><p>{contractStr}</p><p>{transferVal}</p><p>{age}</p>";
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
