using fmassman.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace fmassman.Api.Functions
{
    public class TacticFunctions
    {
        private readonly ITacticRepository _repository;
        private readonly ILogger<TacticFunctions> _logger;

        public TacticFunctions(ITacticRepository repository, ILogger<TacticFunctions> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        [Function("GetTactics")]
        public async Task<IActionResult> GetTactics(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "tactics")] HttpRequest req)
        {
            _logger.LogInformation("Getting all tactics.");
            var tactics = await _repository.GetAllAsync();
            return new OkObjectResult(tactics);
        }

        [Function("SaveTactic")]
        public async Task<IActionResult> SaveTactic(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "tactics")] HttpRequest req)
        {
            _logger.LogInformation("Saving tactic.");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            if (string.IsNullOrWhiteSpace(requestBody))
            {
                return new BadRequestObjectResult("Request body is empty.");
            }

            try 
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                var tactic = JsonSerializer.Deserialize<Tactic>(requestBody, options);

                if (tactic == null)
                {
                    return new BadRequestObjectResult("Invalid tactic data.");
                }

                if (string.IsNullOrWhiteSpace(tactic.Name))
                {
                   return new BadRequestObjectResult("Tactic name is required.");
                }

                await _repository.SaveAsync(tactic);
                return new OkResult();
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Error deserializing tactic.");
                return new BadRequestObjectResult("Invalid JSON format.");
            }
        }

        [Function("DeleteTactic")]
        public async Task<IActionResult> DeleteTactic(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "tactics/{id}")] HttpRequest req, string id)
        {
            _logger.LogInformation($"Deleting tactic with id: {id}");
            await _repository.DeleteAsync(id);
            return new OkResult();
        }
    }
}
