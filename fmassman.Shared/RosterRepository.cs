using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace fmassman.Shared
{
    public class RosterRepository : IRosterRepository
    {
        private readonly string _filePath;

        public RosterRepository(string filePath)
        {
            _filePath = filePath;
        }

        public List<PlayerImportData> Load()
        {
            if (!File.Exists(_filePath))
            {
                return new List<PlayerImportData>();
            }

            string json = File.ReadAllText(_filePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<PlayerImportData>();
            }
            return JsonSerializer.Deserialize<List<PlayerImportData>>(json) ?? new List<PlayerImportData>();
        }

        public void Save(List<PlayerImportData> players)
        {
            string json = JsonSerializer.Serialize(players, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
        }

        public void Delete(string playerName)
        {
            if (!File.Exists(_filePath)) return;

            var players = Load();
            var playerToRemove = players.FirstOrDefault(p => p.PlayerName.Equals(playerName, System.StringComparison.OrdinalIgnoreCase));

            if (playerToRemove != null)
            {
                players.Remove(playerToRemove);
                Save(players);
            }
        }
    }
}
