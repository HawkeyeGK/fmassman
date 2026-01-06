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
            var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var position = System.Text.Json.JsonSerializer.Deserialize<PositionDefinition>(requestBody, options);

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
            var results = new Dictionary<string, string>();
            
            // 1. Test Logger
            try { _logger.LogInformation("Test in PositionFunctions called!"); results["Logger"] = "OK"; }
            catch (Exception ex) { results["Logger"] = ex.Message; }

            // 2. Test HttpClientFactory
            try 
            { 
                var client = _httpClientFactory.CreateClient("MiroAuth"); 
                results["HttpClientFactory"] = client != null ? "OK" : "Returned NULL";
            }
            catch (Exception ex) { results["HttpClientFactory"] = ex.Message; }

            // 3. Test SettingsRepository
            try 
            { 
                // Just check for null injection
                results["SettingsRepository"] = _settingsRepository != null ? "OK (Injected)" : "NULL";
            }
            catch (Exception ex) { results["SettingsRepository"] = ex.Message; }

             // 4. Test Newtonsoft.Json
            try 
            { 
                var json = "{\"test\": \"value\"}";
                var obj = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(json);
                results["Newtonsoft.Json"] = obj != null ? "OK" : "Failed";
            }
            catch (Exception ex) { results["Newtonsoft.Json"] = ex.Message; }

            // 5. Test Request Object (CRITICAL DIAGNOSTIC)
            try
            {
                var testQuery = req.Query["test"].ToString();
                results["RequestQuery"] = $"Accessed OK. Value: {testQuery ?? "NULL"}";
            }
            catch (Exception ex)
            {
                results["RequestQuery"] = $"CRITICAL FAILURE: {ex.GetType().Name} - {ex.Message}";
            }

            return new OkObjectResult(results);
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
