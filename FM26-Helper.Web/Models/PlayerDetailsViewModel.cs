using FM26_Helper.Shared;
using FM26_Helper.Web.Helpers;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Linq;

namespace FM26_Helper.Web.Models
{
    public class PlayerDetailsViewModel
    {
        private readonly RosterRepository _rosterRepository;
        private readonly IConfiguration _configuration;

        public PlayerImportData? Player { get; private set; }
        public PlayerAnalysis? Analysis { get; private set; }
        public RosterItemViewModel? HeaderData { get; private set; }

        public PlayerDetailsViewModel(RosterRepository rosterRepository, IConfiguration configuration)
        {
            _rosterRepository = rosterRepository;
            _configuration = configuration;
        }


        public bool IsInPossessionMode { get; set; } = true;

        public HeatmapColorScale? GlobalInPossessionScale { get; private set; }
        public HeatmapColorScale? GlobalOutPossessionScale { get; private set; }

        public List<string> MatrixHeaders { get; private set; } = new();
        public List<List<RoleFitResult?>> MatrixRows { get; private set; } = new();

        public void BuildMatrix()
        {
            if (Analysis == null) return;

            var source = IsInPossessionMode ? Analysis.InPossessionFits : Analysis.OutPossessionFits;
            if (source == null || !source.Any())
            {
                MatrixHeaders.Clear();
                MatrixRows.Clear();
                return;
            }

            // 1. Group by Category (Columns)
            var groups = source
                .GroupBy(r => r.Category)
                .OrderBy(g => g.Key)
                .ToList();

            // 2. Set Headers
            MatrixHeaders = groups.Select(g => g.Key).ToList();

            // 3. Determine max rows needed
            int maxRows = groups.Any() ? groups.Max(g => g.Count()) : 0;
            
            // 4. Build Rows (Transpose)
            MatrixRows.Clear();
            for (int i = 0; i < maxRows; i++)
            {
                var row = new List<RoleFitResult?>();
                foreach (var group in groups)
                {
                    // Order roles in group by Score Descending
                    var role = group.OrderByDescending(r => r.Score).ElementAtOrDefault(i);
                    row.Add(role);
                }
                MatrixRows.Add(row);
            }
        }

        public void LoadPlayer(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                Player = null;
                return;
            }

            var path = _configuration["RosterFilePath"];
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            var allPlayers = _rosterRepository.Load(path);
            Player = allPlayers.FirstOrDefault(p => p.PlayerName.Equals(name, System.StringComparison.OrdinalIgnoreCase));

            if (Player != null)
            {
                if (Player.Snapshot != null)
                {
                    Analysis = PlayerAnalyzer.Analyze(Player.Snapshot);
                    HeaderData = RosterItemViewModel.FromPlayer(Player);
                    
                    BuildMatrix();
                }
            }

            // Calculate Global Scales if we have any players
            if (allPlayers.Any())
            {
                var globalAnalyses = allPlayers
                    .Where(p => p.Snapshot != null)
                    .Select(p => PlayerAnalyzer.Analyze(p.Snapshot!))
                    .ToList();

                if (globalAnalyses.Any())
                {
                    var allInScores = globalAnalyses.SelectMany(a => a.InPossessionFits).Select(r => r.Score);
                    var allOutScores = globalAnalyses.SelectMany(a => a.OutPossessionFits).Select(r => r.Score);

                    if (allInScores.Any())
                        GlobalInPossessionScale = new HeatmapColorScale(allInScores.Min(), allInScores.Max());
                    
                    if (allOutScores.Any())
                        GlobalOutPossessionScale = new HeatmapColorScale(allOutScores.Min(), allOutScores.Max());
                }
            }
        }
    }
}
