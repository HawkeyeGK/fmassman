using System.Collections.Generic;
using fmassman.Shared;

namespace fmassman.Shared.Services
{
    public interface IRoleService
    {
        void Initialize();
        Task<List<RoleDefinition>> LoadLocalRolesAsync();
        Task SaveRolesAsync(List<RoleDefinition> roles);
        void ResetToBaseline();
    }
}
