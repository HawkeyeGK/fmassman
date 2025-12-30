using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace fmassman.Shared
{
    public class RosterRepository : IRosterRepository
    {
        private readonly string _filePath;

        public RosterRepository(string filePath)
        {
            _filePath = filePath;
        }

        public Task<List<PlayerImportData>> LoadAsync()
        {
            if (!File.Exists(_filePath))
            {
                return Task.FromResult(new List<PlayerImportData>());
            }

            string json = File.ReadAllText(_filePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return Task.FromResult(new List<PlayerImportData>());
            }
            return Task.FromResult(JsonSerializer.Deserialize<List<PlayerImportData>>(json) ?? new List<PlayerImportData>());
        }

        public Task SaveAsync(List<PlayerImportData> players)
        {
            string json = JsonSerializer.Serialize(players, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
            return Task.CompletedTask;
        }

        public async Task DeleteAsync(string playerName)
        {
            if (!File.Exists(_filePath)) return;

            var players = await LoadAsync();
            var playerToRemove = players.FirstOrDefault(p => p.PlayerName.Equals(playerName, System.StringComparison.OrdinalIgnoreCase));

            if (playerToRemove != null)
            {
                players.Remove(playerToRemove);
                await SaveAsync(players);
            }
        }

        public async Task UpsertAsync(PlayerImportData player)
        {
            var players = await LoadAsync();
            var existing = players.FirstOrDefault(p => p.PlayerName.Equals(player.PlayerName, System.StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                players.Remove(existing);
            }
            players.Add(player);
            await SaveAsync(players);
        }
    }
}
