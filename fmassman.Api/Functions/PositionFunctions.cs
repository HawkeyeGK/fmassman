using System;
using System.IO;
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

        public PositionFunctions(ILogger<PositionFunctions> logger, IPositionRepository repository)
        {
            _logger = logger;
            _repository = repository;
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

                return new ContentResult 
                { 
                    Content = $"SUCCESS! Would redirect to Miro with ClientId: {clientId.Substring(0, 5)}...",
                    ContentType = "text/plain",
                    StatusCode = 200
                };
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
    }
}
