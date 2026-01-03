using System.Collections.Generic;
using System.Threading.Tasks;
using fmassman.Shared.Models;

namespace fmassman.Client.Services
{
    public interface IPositionService
    {
        Task<List<PositionDefinition>> GetAllAsync();
        Task UpsertAsync(PositionDefinition position);
        Task DeleteAsync(string id);
    }
}
