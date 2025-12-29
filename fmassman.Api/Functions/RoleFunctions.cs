using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using fmassman.Shared;
using fmassman.Shared.Services;

namespace fmassman.Api.Functions
{
    public class RoleFunctions
    {
        private readonly ILogger<RoleFunctions> _logger;
        private readonly IRoleService _roleService;

        public RoleFunctions(ILogger<RoleFunctions> logger, IRoleService roleService)
        {
            _logger = logger;
            _roleService = roleService;
        }

        [Function("GetRoles")]
        public async Task<IActionResult> GetRoles(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "roles")] HttpRequest req)
        {
            _logger.LogInformation("Processing GetRoles request.");
            var roles = await _roleService.LoadLocalRolesAsync();
            return new OkObjectResult(roles);
        }

        [Function("SaveRoles")]
        public async Task<IActionResult> SaveRoles(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "roles")] HttpRequest req,
            [FromBody] List<RoleDefinition> roles)
        {
            _logger.LogInformation("Processing SaveRoles request with {Count} roles.", roles?.Count ?? 0);

            if (roles == null)
            {
                return new BadRequestObjectResult("Invalid payload");
            }

            await _roleService.SaveRolesAsync(roles);
            return new OkResult();
        }

        [Function("ResetRoles")]
        public async Task<IActionResult> ResetRoles(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "roles/reset")] HttpRequest req)
        {
            _logger.LogInformation("Processing ResetRoles request.");
            await _roleService.ResetToBaselineAsync();
            return new OkResult();
        }
    }
}
