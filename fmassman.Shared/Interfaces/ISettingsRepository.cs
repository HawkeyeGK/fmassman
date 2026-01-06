using System.Threading.Tasks;
using fmassman.Shared.Models;

namespace fmassman.Shared.Interfaces
{
    public interface ISettingsRepository
    {
        Task UpsertMiroTokensAsync(MiroTokenSet tokens);
    }
}
