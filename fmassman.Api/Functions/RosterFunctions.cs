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
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "roster")] HttpRequest req,
            [FromBody] List<PlayerImportData> players)
        {
            _logger.LogInformation("Processing SaveRoster request with {Count} players.", players?.Count ?? 0);

            if (players == null)
            {
                _logger.LogWarning("SaveRoster: players payload is NULL");
                return new BadRequestObjectResult("Invalid payload");
            }

            // DEBUG: Log Goalkeeping data for each player to trace deserialization issues
            foreach (var player in players)
            {
                var gk = player.Snapshot?.Goalkeeping;
                if (gk != null)
                {
                    _logger.LogInformation("SaveRoster: Player {Name} - GK Handling={Handling}, Reflexes={Reflexes}, AerialReach={AerialReach}",
                        player.PlayerName, gk.Handling, gk.Reflexes, gk.AerialReach);
                }
                else
                {
                    _logger.LogWarning("SaveRoster: Player {Name} - Goalkeeping is NULL (Snapshot null? {SnapshotNull})",
                        player.PlayerName, player.Snapshot == null);
                }
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
