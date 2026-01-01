using fmassman.Shared;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace fmassman.Client.Services
{
    public interface ITacticService
    {
        Task<List<Tactic>> GetAllAsync();
        Task SaveAsync(Tactic tactic);
        Task DeleteAsync(string id);
        Task<Tactic?> GetByIdAsync(string id);
    }
}
