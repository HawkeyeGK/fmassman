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
                throw new UnauthorizedAccessException("Miro tokens not found.");
            }

            if (tokens.ExpiresAt <= DateTime.UtcNow)
            {
                _logger.LogWarning("Miro access token has expired at {Expiry}.", tokens.ExpiresAt);
                // In a real scenario, we would refresh here. For now, strictly V2 or fail.
                throw new UnauthorizedAccessException("Miro access token has expired.");
            }

            var client = _httpClientFactory.CreateClient("MiroAuth");
            // BaseAddress is already set to https://api.miro.com/ in Program.cs, but we want to ensure V2 usage.
            // Program.cs has: client.BaseAddress = new Uri("https://api.miro.com/");
            // We'll append v2/ segments in the requests.
            
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokens.AccessToken);
            
            return client;
        }

        [Function("MiroRegisterSchema")]
        public async Task<IActionResult> RegisterSchema(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "miro/schema/register")] HttpRequestData req)
        {
            try
            {
                var client = await GetAuthenticatedClientAsync();
                var boardId = Environment.GetEnvironmentVariable("MiroBoardId") ?? "uXjVGUR-CSw=";

                // 1. Get Team ID (Optional check, but good for validation if needed, mostly we just need boardId for the URL)
                // The spec says "Get the Team ID... to find the teamId". Actually creating app_card_schemas is usually done on the team level OR board level?
                // Miro V2 Docs: POST https://api.miro.com/v2/boards/{board_id}/app_card_schemas
                // So we assume we just post to the board.

                var schemaPayload = new
                {
                    fields = new[]
                    {
                        new { key = "position", type = "string", label = "Position" },
                        new { key = "age", type = "number", label = "Age" },
                        new { key = "contract", type = "string", label = "Contract Exp" },
                        new { key = "role", type = "string", label = "Best Role" },
                        new { key = "fit", type = "string", label = "Fit %" }
                    }
                };

                var response = await client.PostAsJsonAsync($"v2/boards/{boardId}/app_card_schemas", schemaPayload);
                
                if (response.StatusCode == HttpStatusCode.Conflict)
                {
                   return new OkObjectResult(new { message = "Schema already exists." });
                }

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to register schema: {StatusCode} {Content}", response.StatusCode, errorContent);
                    return new BadRequestObjectResult(new { error = $"Failed to register schema: {response.StatusCode}", details = errorContent });
                }

                var result = await response.Content.ReadFromJsonAsync<object>();
                return new OkObjectResult(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating Miro schema");
                return new ObjectResult(new { error = ex.Message }) { StatusCode = 500 };
            }
        }

        [Function("MiroCreateTestCard")]
        public async Task<IActionResult> CreateTestCard(
             [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "miro/card/test")] HttpRequestData req)
        {
            try
            {
                var client = await GetAuthenticatedClientAsync();
                var boardId = Environment.GetEnvironmentVariable("MiroBoardId") ?? "uXjVGUR-CSw=";

                var cardPayload = new
                {
                    data = new
                    {
                        title = "Test Player (Pep Guardiola)",
                        status = "connected"
                    },
                    style = new
                    {
                        fillColor = "#0099FF"
                    },
                    fields = new object[]
                    {
                        new { value = "GK", key = "position" },
                        new { value = 24, key = "age" }, // Sent as number to match schema type
                        new { value = "2026-06-30", key = "contract" },
                        new { value = "Sweeper Keeper", key = "role" },
                        new { value = "98%", key = "fit" }
                    }
                };
                
                // Note: 'fields' in the specific card creation payload is array of objects {value, key} 
                // ONLY IF the schema is registered. If using basic app cards without schema, fields might be different,
                // but the goal here is to use the App Card Schema.
                
                var response = await client.PostAsJsonAsync($"v2/boards/{boardId}/app_cards", cardPayload);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to create test card: {StatusCode} {Content}", response.StatusCode, errorContent);
                    return new BadRequestObjectResult(new { error = $"Failed to create card: {response.StatusCode}", details = errorContent });
                }

                var result = await response.Content.ReadFromJsonAsync<object>();
                return new OkObjectResult(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating Miro test card");
                return new ObjectResult(new { error = ex.Message }) { StatusCode = 500 };
            }
        }
    }
}
