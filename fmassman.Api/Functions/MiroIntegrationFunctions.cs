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
using Newtonsoft.Json;

namespace fmassman.Api.Functions
{
    public class MiroIntegrationFunctions
    {
        private readonly ILogger<MiroIntegrationFunctions> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ISettingsRepository _settingsRepository;

        public MiroIntegrationFunctions(ILogger<MiroIntegrationFunctions> logger, 
            IHttpClientFactory httpClientFactory, ISettingsRepository settingsRepository)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _settingsRepository = settingsRepository;
        }

        [Function("MiroLogin")]
        public IActionResult MiroLogin([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "diagnostic/miro/login")] HttpRequest req)
        {
            _logger.LogInformation("MiroLogin called");
            var clientId = Environment.GetEnvironmentVariable("MiroClientId");
            var redirectUri = Environment.GetEnvironmentVariable("MiroRedirectUrl");

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(redirectUri))
            {
                return new ContentResult { Content = "Missing Miro Env Vars", StatusCode = 500 };
            }

            var url = $"https://miro.com/oauth/authorize?response_type=code&client_id={clientId}&redirect_uri={System.Net.WebUtility.UrlEncode(redirectUri)}";
            return new RedirectResult(url, false);
        }

        [Function("MiroExchange")]
        public async Task<IActionResult> MiroExchange([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "miro/exchange")] HttpRequest req)
        {
            try
            {
                _logger.LogInformation("MiroExchange POST called");
                
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var data = JsonConvert.DeserializeObject<MiroExchangeRequest>(requestBody);
                string code = data?.Code;

                if (string.IsNullOrEmpty(code)) return new BadRequestObjectResult("No code provided in body");

                var clientId = Environment.GetEnvironmentVariable("MiroClientId");
                var clientSecret = Environment.GetEnvironmentVariable("MiroClientSecret");
                // IMPORTANT: This must match the URI registered in Miro and the one the Client used.
                // Since we are now using the Client-Side route, the Env Var should be updated to:
                // https://www.fmassman.com/miro/finalize
                var redirectUri = Environment.GetEnvironmentVariable("MiroRedirectUrl");

                var client = _httpClientFactory.CreateClient("MiroAuth");
                var values = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("grant_type", "authorization_code"),
                    new KeyValuePair<string, string>("client_id", clientId),
                    new KeyValuePair<string, string>("client_secret", clientSecret),
                    new KeyValuePair<string, string>("code", code),
                    new KeyValuePair<string, string>("redirect_uri", redirectUri)
                };

                var response = await client.PostAsync("https://api.miro.com/v1/oauth/token", new FormUrlEncodedContent(values));
                if (!response.IsSuccessStatusCode)
                {
                    var err = await response.Content.ReadAsStringAsync();
                    return new ObjectResult($"Miro Error: {response.StatusCode} - {err}") { StatusCode = 502 };
                }

                var json = await response.Content.ReadAsStringAsync();
                var tokenDto = JsonConvert.DeserializeObject<MiroTokenResponse>(json);
                 
                var tokens = new MiroTokenSet
                {
                    AccessToken = tokenDto.access_token,
                    RefreshToken = tokenDto.refresh_token,
                    Scope = tokenDto.scope,
                    ExpiresAt = DateTime.UtcNow.AddSeconds(tokenDto.expires_in)
                };

                await _settingsRepository.UpsertMiroTokensAsync(tokens);

                return new OkObjectResult(new { status = "success" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Miro Exchange Failed");
                return new ObjectResult($"Exception: {ex.Message}\nStack: {ex.StackTrace}") { StatusCode = 500 };
            }
        }

        public class MiroExchangeRequest
        {
            public string Code { get; set; }
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
