using System.Collections.Generic;
using System.Threading.Tasks;
using fmassman.Shared.Models;

namespace fmassman.Shared.Interfaces
{
    public interface IPositionRepository
    {
        Task<List<PositionDefinition>> GetAllAsync();
        Task<PositionDefinition?> GetByIdAsync(string id);
        Task UpsertAsync(PositionDefinition position);
        Task DeleteAsync(string id);
    }
}
