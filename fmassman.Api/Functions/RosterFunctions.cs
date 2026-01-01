using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using fmassman.Shared;
using fmassman.Shared.Services;

namespace fmassman.Api.Functions
{
    public class RosterFunctions
    {
        private readonly ILogger<RosterFunctions> _logger;
        private readonly IRosterRepository _repository;

        public RosterFunctions(ILogger<RosterFunctions> logger, IRosterRepository repository)
        {
            _logger = logger;
            _repository = repository;
        }

        [Function("GetRoster")]
        public async Task<IActionResult> GetRoster(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "roster")] HttpRequest req)
        {
            _logger.LogInformation("Processing GetRoster request.");
            var roster = await _repository.LoadAsync();
            return new OkObjectResult(roster);
        }

        [Function("SaveRoster")]
        public async Task<IActionResult> SaveRoster(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "roster")] HttpRequest req)
        {
            _logger.LogInformation("Processing SaveRoster request.");

            // Manually deserialize with case-insensitive options
            // [FromBody] doesn't respect configured JSON options in Azure Functions
            List<PlayerImportData>? players = null;
            try
            {
                var options = new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                players = await System.Text.Json.JsonSerializer.DeserializeAsync<List<PlayerImportData>>(
                    req.Body, options);
            }
            catch (System.Text.Json.JsonException ex)
            {
                _logger.LogError(ex, "SaveRoster: Failed to deserialize request body");
                return new BadRequestObjectResult($"Invalid JSON: {ex.Message}");
            }

            _logger.LogInformation("SaveRoster: Deserialized {Count} players.", players?.Count ?? 0);

            if (players == null || players.Count == 0)
            {
                _logger.LogWarning("SaveRoster: players payload is NULL or empty");
                return new BadRequestObjectResult("Invalid payload - no players provided");
            }



            await _repository.SaveAsync(players);
            _logger.LogInformation("SaveRoster: Save completed successfully");
            return new OkResult();
        }

        [Function("DeletePlayer")]
        public async Task<IActionResult> DeletePlayer(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "roster/{playerName}")] HttpRequest req,
            string playerName)
        {
            _logger.LogInformation("Processing DeletePlayer request for '{PlayerName}'.", playerName);
            
            if (string.IsNullOrWhiteSpace(playerName))
            {
                return new BadRequestObjectResult("Player name is required");
            }

            await _repository.DeleteAsync(playerName);
            return new OkResult();
        }
    }
}
