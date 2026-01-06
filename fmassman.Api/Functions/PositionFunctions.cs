using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using fmassman.Shared.Interfaces;
using fmassman.Shared.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace fmassman.Api.Functions
{
    public class PositionFunctions
    {
        private readonly ILogger<PositionFunctions> _logger;
        private readonly IPositionRepository _repository;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ISettingsRepository _settingsRepository;

        public PositionFunctions(ILogger<PositionFunctions> logger, IPositionRepository repository, 
            IHttpClientFactory httpClientFactory, ISettingsRepository settingsRepository)
        {
            _logger = logger;
            _repository = repository;
            _httpClientFactory = httpClientFactory;
            _settingsRepository = settingsRepository;
        }

        [Function("GetPositions")]
        public async Task<IActionResult> GetPositions([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "positions")] HttpRequest req)
        {
            try
            {
                var positions = await _repository.GetAllAsync();
                return new OkObjectResult(positions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting positions");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }

        [Function("UpsertPosition")]
        public async Task<IActionResult> UpsertPosition([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "positions")] HttpRequest req)
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var position = JsonSerializer.Deserialize<PositionDefinition>(requestBody, options);

            if (position == null)
            {
                return new BadRequestObjectResult("Invalid position data.");
            }

            await _repository.UpsertAsync(position);
            return new OkResult();
        }

        [Function("DeletePosition")]
        public async Task<IActionResult> DeletePosition([HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "positions/{id}")] HttpRequest req, string id)
        {
            try
            {
                await _repository.DeleteAsync(id);
                return new OkResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting position {PositionId}", id);
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }

        [Function("TestInPositions")]
        public IActionResult TestInPositions([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "positionstest")] HttpRequest req)
        {
            _logger.LogInformation("Test in PositionFunctions called!");
            return new OkObjectResult("Test in existing file successful!");
        }

        // DIAGNOSTIC: Testing if Miro Login works when in existing file
        [Function("MiroLoginDiagnostic")]
        public IActionResult MiroLoginDiagnostic([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "diagnostic/miro/login")] HttpRequest req)
        {
            try
            {
                _logger.LogInformation("Diagnostic Miro login called");
                
                string clientId = null;
                string redirectUri = null;
                
                try
                {
                    clientId = Environment.GetEnvironmentVariable("MiroClientId");
                    redirectUri = Environment.GetEnvironmentVariable("MiroRedirectUrl");
                }
                catch (Exception envEx)
                {
                    return new ContentResult 
                    { 
                        Content = $"ERROR reading env vars: {envEx.Message}",
                        ContentType = "text/plain",
                        StatusCode = 500
                    };
                }

                if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(redirectUri))
                {
                    return new ContentResult 
                    { 
                        Content = $"Missing Miro config. ClientId: '{clientId ?? "NULL"}', RedirectUri: '{redirectUri ?? "NULL"}'",
                        ContentType = "text/plain",
                        StatusCode = 200
                    };
                }

                var miroAuthUrl = $"https://miro.com/oauth/authorize?response_type=code&client_id={clientId}&redirect_uri={System.Net.WebUtility.UrlEncode(redirectUri)}";
                
                _logger.LogInformation($"Redirecting to Miro: {miroAuthUrl}");
                return new RedirectResult(miroAuthUrl, false);
            }
            catch (Exception ex)
            {
                return new ContentResult 
                { 
                    Content = $"CAUGHT EXCEPTION: {ex.GetType().Name}: {ex.Message}\n\nStack: {ex.StackTrace}",
                    ContentType = "text/plain",
                    StatusCode = 500
                };
            }
        }

        [Function("MiroCallbackDiagnostic")]
        public async Task<IActionResult> MiroCallbackDiagnostic([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "miro/auth/callback")] HttpRequest req)
        {
            try
            {
                _logger.LogInformation($"Miro callback received. Query: {req.QueryString}");
                
                var code = req.Query["code"].ToString();

                if (string.IsNullOrEmpty(code))
                {
                    _logger.LogError($"Missing code parameter. Query string was: {req.QueryString}");
                    return new ContentResult 
                    { 
                        Content = $"Missing code parameter. Query: {req.QueryString}",
                        ContentType = "text/plain",
                        StatusCode = 400
                    };
                }

                var clientId = Environment.GetEnvironmentVariable("MiroClientId");
                var clientSecret = Environment.GetEnvironmentVariable("MiroClientSecret");
                var redirectUri = Environment.GetEnvironmentVariable("MiroRedirectUrl");

                if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret) || string.IsNullOrEmpty(redirectUri))
                {
                    _logger.LogError("Missing Miro configuration.");
                    return new ContentResult 
                    { 
                        Content = "Server configuration error - missing Miro credentials",
                        ContentType = "text/plain",
                        StatusCode = 500
                    };
                }

                // Exchange code for tokens via Miro API
                var httpClient = _httpClientFactory.CreateClient();
                var values = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("grant_type", "authorization_code"),
                    new KeyValuePair<string, string>("client_id", clientId),
                    new KeyValuePair<string, string>("client_secret", clientSecret),
                    new KeyValuePair<string, string>("code", code),
                    new KeyValuePair<string, string>("redirect_uri", redirectUri)
                };

                var content = new FormUrlEncodedContent(values);
                var tokenResponse = await httpClient.PostAsync("https://api.miro.com/v1/oauth/token", content);

                if (!tokenResponse.IsSuccessStatusCode)
                {
                    var errorContent = await tokenResponse.Content.ReadAsStringAsync();
                    _logger.LogError($"Error exchanging token: {tokenResponse.StatusCode} - {errorContent}");
                    return new ContentResult 
                    { 
                        Content = $"Failed to exchange token with Miro: {tokenResponse.StatusCode}",
                        ContentType = "text/plain",
                        StatusCode = 502
                    };
                }

                var jsonContent = await tokenResponse.Content.ReadAsStringAsync();
                var tokenDto = Newtonsoft.Json.JsonConvert.DeserializeObject<MiroTokenResponse>(jsonContent);

                if (tokenDto == null || string.IsNullOrEmpty(tokenDto.access_token))
                {
                    _logger.LogError($"Failed to deserialize Miro token response. Response: {jsonContent}");
                    return new ContentResult 
                    { 
                        Content = "Invalid token response from Miro",
                        ContentType = "text/plain",
                        StatusCode = 502
                    };
                }

                // Save tokens to Cosmos DB
                var tokens = new MiroTokenSet
                {
                    AccessToken = tokenDto.access_token,
                    RefreshToken = tokenDto.refresh_token,
                    Scope = tokenDto.scope,
                    ExpiresAt = DateTime.UtcNow.AddSeconds(tokenDto.expires_in)
                };

                await _settingsRepository.UpsertMiroTokensAsync(tokens);
                _logger.LogInformation("Successfully saved Miro tokens to Cosmos DB");

                return new RedirectResult("/admin/positions?status=success", false);
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Unhandled exception in Miro callback");
                return new ContentResult 
                { 
                    Content = $"EXCEPTION in callback: {ex.GetType().Name}: {ex.Message}\n\nStack: {ex.StackTrace}",
                    ContentType = "text/plain",
                    StatusCode = 500
                };
            }
        }

        public class MiroTokenResponse
        {
            public string access_token { get; set; } = "";
            public string refresh_token { get; set; } = "";
            public int expires_in { get; set; }
            public string scope { get; set; } = "";
        }
    }
}
