using System.Collections.Generic;
using fmassman.Shared;

namespace fmassman.Shared.Services
{
    public interface IRoleService
    {
        void Initialize();
        List<RoleDefinition> LoadLocalRoles();
        void SaveRoles(List<RoleDefinition> roles);
        void ResetToBaseline();
    }
}
