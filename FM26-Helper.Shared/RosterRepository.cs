using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace FM26_Helper.Shared
{
    public class RosterRepository
    {
        public List<PlayerImportData> Load(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return new List<PlayerImportData>();
            }

            string json = File.ReadAllText(filePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<PlayerImportData>();
            }
            return JsonSerializer.Deserialize<List<PlayerImportData>>(json) ?? new List<PlayerImportData>();
        }

        public void Save(string filePath, List<PlayerImportData> players)
        {
            string json = JsonSerializer.Serialize(players, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
        }

        public void Delete(string filePath, string playerName)
        {
            if (!File.Exists(filePath)) return;

            var players = Load(filePath);
            var playerToRemove = players.FirstOrDefault(p => p.PlayerName.Equals(playerName, System.StringComparison.OrdinalIgnoreCase));

            if (playerToRemove != null)
            {
                players.Remove(playerToRemove);
                Save(filePath, players);
            }
        }
    }
}
