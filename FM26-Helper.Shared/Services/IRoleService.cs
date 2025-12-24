using System.Collections.Generic;
using FM26_Helper.Shared;

namespace FM26_Helper.Shared.Services
{
    public interface IRoleService
    {
        void Initialize();
        List<RoleDefinition> LoadLocalRoles();
        void SaveRoles(List<RoleDefinition> roles);
        void ResetToBaseline();
    }
}
