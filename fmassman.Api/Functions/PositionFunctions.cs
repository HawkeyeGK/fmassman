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
    }
}
