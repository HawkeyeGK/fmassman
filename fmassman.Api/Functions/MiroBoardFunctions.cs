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

                // V2 App Cards do not require explicit schema registration in the same way as V1 or Web SDK.
                // Instead of registering a schema (which returns 405), we will verify connection to the board.
                
                var response = await client.GetAsync($"v2/boards/{boardId}");
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to access board: {StatusCode} {Content}", response.StatusCode, errorContent);
                    return new BadRequestObjectResult(new { error = $"Failed to access board: {response.StatusCode}", details = errorContent });
                }

                var board = await response.Content.ReadFromJsonAsync<object>();
                return new OkObjectResult(new 
                { 
                    message = "Connection successful. Schema registration is not required/supported for V2 App Cards; using ad-hoc fields.",
                    boardDetails = board
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking Miro connection");
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
                        new { value = "GK", tooltip = "Position" },
                        new { value = "24", tooltip = "Age" }, // Value must be string? API docs say string. Let's force string to be safe.
                        new { value = "2026-06-30", tooltip = "Contract Exp" },
                        new { value = "Sweeper Keeper", tooltip = "Best Role" },
                        new { value = "98%", tooltip = "Fit %" }
                    }
                };
                
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
