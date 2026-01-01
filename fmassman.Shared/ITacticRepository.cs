using System.Collections.Generic;
using System.Threading.Tasks;

namespace fmassman.Shared
{
    public interface ITacticRepository
    {
        Task<List<Tactic>> GetAllAsync();
        Task SaveAsync(Tactic tactic);
        Task DeleteAsync(string id);
    }
}
