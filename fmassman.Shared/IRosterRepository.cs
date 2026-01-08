using System.Collections.Generic;

namespace fmassman.Shared
{
    public interface IRosterRepository
    {
        Task<List<PlayerImportData>> LoadAsync();
        Task SaveAsync(List<PlayerImportData> players);
        Task DeleteAsync(string playerName);
        Task UpsertAsync(PlayerImportData player);
        Task UpdatePlayerTagsAsync(string playerName, List<string> tagIds);
        Task UpdatePlayerPositionAsync(string playerName, string? positionId);
        Task<PlayerImportData?> GetByIdAsync(string id);
        Task<bool> PushToMiroAsync(string playerId);
    }
}
