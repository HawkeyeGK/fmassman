using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using fmassman.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace fmassman.Api.Functions
{
    public class TagFunctions
    {
        private readonly ILogger<TagFunctions> _logger;
        private readonly ITagRepository _repository;

        public TagFunctions(ILogger<TagFunctions> logger, ITagRepository repository)
        {
            _logger = logger;
            _repository = repository;
        }

        [Function("GetTags")]
        public async Task<IActionResult> GetTags([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tags")] HttpRequest req)
        {
            var tags = await _repository.GetAllAsync();
            return new OkObjectResult(tags);
        }

        [Function("SaveTag")]
        public async Task<IActionResult> SaveTag([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "tags")] HttpRequest req)
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var tag = JsonConvert.DeserializeObject<TagDefinition>(requestBody);

            if (tag == null)
            {
                return new BadRequestObjectResult("Invalid tag data.");
            }

            await _repository.SaveAsync(tag);
            return new OkResult();
        }

        [Function("DeleteTag")]
        public async Task<IActionResult> DeleteTag([HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "tags/{id}")] HttpRequest req, string id)
        {
            try
            {
                await _repository.DeleteAsync(id);
                return new OkResult();
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Failed to delete tag {TagId}", id);
                return new ConflictObjectResult(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting tag {TagId}", id);
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }
    }
}
