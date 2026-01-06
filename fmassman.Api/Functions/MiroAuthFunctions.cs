using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using fmassman.Shared.Interfaces;
using fmassman.Shared.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace fmassman.Api.Functions
{
    public class MiroAuthFunctions
    {
        private readonly ILogger<MiroAuthFunctions> _logger;
        private readonly ISettingsRepository _settingsRepository;
        private readonly IHttpClientFactory _httpClientFactory;

        public MiroAuthFunctions(ILogger<MiroAuthFunctions> logger, ISettingsRepository settingsRepository, IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _settingsRepository = settingsRepository;
            _httpClientFactory = httpClientFactory;
        }

        [Function("MiroLogin")]
        public IActionResult Login([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "miro/auth/login")] HttpRequest req)
        {
            var clientId = Environment.GetEnvironmentVariable("MiroClientId");
            var redirectUri = Environment.GetEnvironmentVariable("MiroRedirectUrl");

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(redirectUri))
            {
                _logger.LogError("Missing Miro configuration (ClientId or RedirectUrl).");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }

            var miroAuthUrl = $"https://miro.com/oauth/authorize?response_type=code&client_id={clientId}&redirect_uri={WebUtility.UrlEncode(redirectUri)}";
            
            return new RedirectResult(miroAuthUrl, false);
        }

        [Function("MiroCallback")]
        public async Task<IActionResult> Callback([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "miro/auth/callback")] HttpRequest req)
        {
            try
            {
                _logger.LogInformation($"Miro callback received. Query: {req.QueryString}");
                
                var code = req.Query["code"].ToString();

                if (string.IsNullOrEmpty(code))
                {
                    _logger.LogError($"Missing code parameter. Query string was: {req.QueryString}");
                    return new BadRequestObjectResult("Missing code parameter.");
                }

                var clientId = Environment.GetEnvironmentVariable("MiroClientId");
                var clientSecret = Environment.GetEnvironmentVariable("MiroClientSecret");
                var redirectUri = Environment.GetEnvironmentVariable("MiroRedirectUrl");

                if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret) || string.IsNullOrEmpty(redirectUri))
                {
                    _logger.LogError("Missing Miro configuration.");
                    return new StatusCodeResult(StatusCodes.Status500InternalServerError);
                }

                var client = _httpClientFactory.CreateClient("MiroAuth");

                var values = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("grant_type", "authorization_code"),
                    new KeyValuePair<string, string>("client_id", clientId),
                    new KeyValuePair<string, string>("client_secret", clientSecret),
                    new KeyValuePair<string, string>("code", code),
                    new KeyValuePair<string, string>("redirect_uri", redirectUri)
                };

                var content = new FormUrlEncodedContent(values);
                var tokenResponse = await client.PostAsync("https://api.miro.com/v1/oauth/token", content);

                if (!tokenResponse.IsSuccessStatusCode)
                {
                    var errorContent = await tokenResponse.Content.ReadAsStringAsync();
                    _logger.LogError($"Error exchanging token: {tokenResponse.StatusCode} - {errorContent}");
                    return new StatusCodeResult(StatusCodes.Status502BadGateway);
                }

                var jsonContent = await tokenResponse.Content.ReadAsStringAsync();
                var tokenDto = JsonConvert.DeserializeObject<MiroTokenResponse>(jsonContent);

                if (tokenDto == null || string.IsNullOrEmpty(tokenDto.access_token))
                {
                    _logger.LogError($"Failed to deserialize Miro token response or empty access token. Response: {jsonContent}");
                    return new StatusCodeResult(StatusCodes.Status502BadGateway);
                }

                var tokens = new MiroTokenSet
                {
                    AccessToken = tokenDto.access_token,
                    RefreshToken = tokenDto.refresh_token,
                    Scope = tokenDto.scope,
                    ExpiresAt = DateTime.UtcNow.AddSeconds(tokenDto.expires_in)
                };

                await _settingsRepository.UpsertMiroTokensAsync(tokens);
                _logger.LogInformation("Successfully upserted Miro tokens to Cosmos DB.");

                return new RedirectResult("/admin/positions?status=success", false);
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Unhandled exception in Miro callback");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
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
