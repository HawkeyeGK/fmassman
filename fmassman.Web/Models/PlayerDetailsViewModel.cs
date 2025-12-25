using fmassman.Shared;
using fmassman.Web.Helpers;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Components;

namespace fmassman.Web.Models
{
    public class PlayerDetailsViewModel
    {
        private readonly IRosterRepository _rosterRepository;
        private readonly NavigationManager _navigationManager;

        public PlayerImportData? Player { get; private set; }
        public PlayerAnalysis? Analysis { get; private set; }
        public RosterItemViewModel? HeaderData { get; private set; }

        public RoleFitResult? BestInPossession => Analysis?.InPossessionFits
            .OrderByDescending(r => r.Score)
            .FirstOrDefault();

        public RoleFitResult? BestOutPossession => Analysis?.OutPossessionFits
            .OrderByDescending(r => r.Score)
            .FirstOrDefault();

        public PlayerDetailsViewModel(IRosterRepository rosterRepository, NavigationManager navigationManager)
        {
            _rosterRepository = rosterRepository;
            _navigationManager = navigationManager;
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
            // Sort by index in CategoryOrder. If not found (-1), put at end (int.MaxValue)
            var groups = source
                .GroupBy(r => r.Category)
                .OrderBy(g => ScoutingConstants.GetCategoryRank(g.Key))
                .ThenBy(g => g.Key) // Secondary sort for unknown categories
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

        public async Task LoadPlayerAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                Player = null;
                return;
            }

            var allPlayers = await _rosterRepository.LoadAsync();
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

        public async Task DeletePlayerAsync()
        {
            if (Player == null) return;

            await _rosterRepository.DeleteAsync(Player.PlayerName);

            // Navigate back to home/roster to clear the invalid state
            _navigationManager.NavigateTo("/", forceLoad: true);
        }
    }
}
