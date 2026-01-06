using System.Net;
using fmassman.Shared.Interfaces;
using fmassman.Shared.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace fmassman.Api.Functions
{
    public class MiroAuthFunctions
    {
        private readonly ILogger _logger;
        private readonly ISettingsRepository _settingsRepository;
        private readonly IHttpClientFactory _httpClientFactory;

        public MiroAuthFunctions(ILoggerFactory loggerFactory, ISettingsRepository settingsRepository, IHttpClientFactory httpClientFactory)
        {
            _logger = loggerFactory.CreateLogger<MiroAuthFunctions>();
            _settingsRepository = settingsRepository;
            _httpClientFactory = httpClientFactory;
        }

        [Function("MiroLogin")]
        public HttpResponseData Login([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "miro/auth/login")] HttpRequestData req)
        {
            var clientId = Environment.GetEnvironmentVariable("MiroClientId");
            var redirectUri = Environment.GetEnvironmentVariable("MiroRedirectUrl");

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(redirectUri))
            {
                _logger.LogError("Missing Miro configuration (ClientId or RedirectUrl).");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                errorResponse.WriteString("Server configuration error.");
                return errorResponse;
            }

            var miroAuthUrl = $"https://miro.com/oauth/authorize?response_type=code&client_id={clientId}&redirect_uri={WebUtility.UrlEncode(redirectUri)}";

            var response = req.CreateResponse(HttpStatusCode.Found);
            response.Headers.Add("Location", miroAuthUrl);
            return response;
        }

        [Function("MiroCallback")]
        public async Task<HttpResponseData> Callback([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "miro/auth/callback")] HttpRequestData req)
        {
            var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            var code = query["code"];

            if (string.IsNullOrEmpty(code))
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                badRequest.WriteString("Missing code parameter.");
                return badRequest;
            }

            var clientId = Environment.GetEnvironmentVariable("MiroClientId");
            var clientSecret = Environment.GetEnvironmentVariable("MiroClientSecret");
            var redirectUri = Environment.GetEnvironmentVariable("MiroRedirectUrl");

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret) || string.IsNullOrEmpty(redirectUri))
            {
                _logger.LogError("Missing Miro configuration.");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                errorResponse.WriteString("Server configuration error.");
                return errorResponse;
            }

            var client = _httpClientFactory.CreateClient("MiroAuth");

            // Prepare token request
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
                var errorResponse = req.CreateResponse(HttpStatusCode.BadGateway);
                errorResponse.WriteString("Failed to authenticate with Miro.");
                return errorResponse;
            }

            var jsonContent = await tokenResponse.Content.ReadAsStringAsync();
            // Assuming the token response maps to our MiroTokenSet properties (access_token, refresh_token, etc.)
            // We need to handle snake_case to PascalCase mapping if using System.Text.Json, or use JsonProperty attributes.
            // Since MiroTokenSet doesn't use JsonProperty for these fields yet, I'll rely on property names or add mapping.
            // Miro returns: access_token, refresh_token, expires_in, scope, token_type, team_id, user_id
            
            // Let's create a DTO for the response or update MiroTokenSet to handle snake_case deserialization
            // For now, I will use dynamic or a quick DTO to map it to MiroTokenSet to be safe.
            
            var tokenDto = JsonConvert.DeserializeObject<MiroTokenResponse>(jsonContent);

            if (tokenDto == null)
            {
                _logger.LogError("Failed to deserialize Miro token response.");
                var errorResponse = req.CreateResponse(HttpStatusCode.BadGateway);
                 errorResponse.WriteString("Invalid response from Miro.");
                return errorResponse;
            }

            var tokens = new MiroTokenSet
            {
                AccessToken = tokenDto.access_token,
                RefreshToken = tokenDto.refresh_token,
                Scope = tokenDto.scope,
                ExpiresAt = DateTime.UtcNow.AddSeconds(tokenDto.expires_in)
            };

            await _settingsRepository.UpsertMiroTokensAsync(tokens);

            // Redirect to success page
            var response = req.CreateResponse(HttpStatusCode.Found);
            // Assuming the client runs at the origin. We might need a config for ClientUrl if it differs.
            // For now, redirect to root/admin/integrations as requested.
            response.Headers.Add("Location", "/admin/integrations?status=success");
            return response;
        }

        private class MiroTokenResponse
        {
            public string access_token { get; set; } = "";
            public string refresh_token { get; set; } = "";
            public int expires_in { get; set; }
            public string scope { get; set; } = "";
        }
    }
}
