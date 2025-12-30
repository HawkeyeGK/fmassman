using System.Collections.Generic;

namespace fmassman.Shared
{
    public interface IRosterRepository
    {
        Task<List<PlayerImportData>> LoadAsync();
        Task SaveAsync(List<PlayerImportData> players);
        Task DeleteAsync(string playerName);
        Task UpsertAsync(PlayerImportData player);
    }
}
