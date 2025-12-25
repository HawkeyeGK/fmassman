using System.Collections.Generic;
using fmassman.Shared;

namespace fmassman.Shared.Services
{
    public interface IRoleService
    {
        Task InitializeAsync();
        Task<List<RoleDefinition>> LoadLocalRolesAsync();
        Task SaveRolesAsync(List<RoleDefinition> roles);
        Task ResetToBaselineAsync();
    }
}
