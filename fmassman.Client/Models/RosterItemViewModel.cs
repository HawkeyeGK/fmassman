namespace fmassman.Client.Models
{
    public class RosterItemViewModel
    {
        public string Name { get; set; } = string.Empty;
        public string SortName { get; set; } = string.Empty;
        public int Age { get; set; }
        public DateTime? GameDate { get; set; }
        public List<string> TagIds { get; set; } = new(); // NEW
        public string? PositionId { get; set; } // NEW
        
        public string Personality { get; set; } = string.Empty;
        public string PlayingTime { get; set; } = string.Empty;
        
        public int TransferValueLow { get; set; }
        public int TransferValueHigh { get; set; }
        public int Wage { get; set; }
        public string ContractExpiry { get; set; } = string.Empty;

        // Tactical Analysis
        public double Speed { get; set; }
        public double DNA { get; set; }
        public double Gegenpress { get; set; }
        public double AggressiveDefense { get; set; }
        public double CautiousDefense { get; set; }
        public double DirectAttack { get; set; }
        public double PossessionAttack { get; set; }

        public string BestInPossessionRole { get; set; } = string.Empty;
        public double BestInPossessionScore { get; set; }
        
        public string BestOutPossessionRole { get; set; } = string.Empty;
        public double BestOutPossessionScore { get; set; }

        // UI Helpers
        public string PersonalityCssClass => GetPersonalityClass(Personality);
        public string PlayingTimeCssClass => GetPlayingTimeClass(PlayingTime);
        public string ContractCssClass => GetContractClass(ContractExpiry, GameDate);

        // Factory Method
        public static RosterItemViewModel FromPlayer(fmassman.Shared.PlayerImportData player)
        {
            if (player.Snapshot == null) return new RosterItemViewModel { Name = player.PlayerName };

            var analysis = fmassman.Shared.PlayerAnalyzer.Analyze(player.Snapshot);
            var bestIn = analysis.InPossessionFits.OrderByDescending(r => r.Score).FirstOrDefault();
            var bestOut = analysis.OutPossessionFits.OrderByDescending(r => r.Score).FirstOrDefault();

            var rawName = player.PlayerName.Trim();
            var lastSpaceIndex = rawName.LastIndexOf(' ');
            var sortName = (lastSpaceIndex > 0)
                ? $"{rawName.Substring(lastSpaceIndex + 1)}, {rawName.Substring(0, lastSpaceIndex)}"
                : rawName;

            return new RosterItemViewModel
            {
                Name = player.PlayerName,
                SortName = sortName,
                Age = player.Snapshot.Age,
                GameDate = ParseSmartDate(player.Snapshot.GameDate),
                TagIds = player.TagIds != null ? new List<string>(player.TagIds) : new List<string>(), // Map TagIds
                PositionId = player.PositionId,
                Personality = player.Snapshot.Personality ?? "",
                PlayingTime = player.Snapshot.PlayingTime ?? "",
                TransferValueLow = player.Snapshot.TransferValueLow,
                TransferValueHigh = player.Snapshot.TransferValueHigh,
                Wage = ParseCurrency(player.Snapshot.Wage),
                ContractExpiry = player.Snapshot.ContractExpiry ?? "",

                Speed = analysis.Speed,
                DNA = analysis.DNA,
                Gegenpress = analysis.Gegenpress,
                AggressiveDefense = analysis.AggressiveDefense,
                CautiousDefense = analysis.CautiousDefense,
                DirectAttack = analysis.DirectAttack,
                PossessionAttack = analysis.PossessionAttack,

                BestInPossessionRole = bestIn?.RoleName ?? "N/A",
                BestInPossessionScore = bestIn?.Score ?? 0,
                BestOutPossessionRole = bestOut?.RoleName ?? "N/A",
                BestOutPossessionScore = bestOut?.Score ?? 0
            };
        }

        // Private Helpers
        private static string GetPersonalityClass(string personality)
        {
            var rank = fmassman.Shared.ScoutingConstants.GetPersonalityRank(personality);

            if (rank < 10) return "fm-status-elite";
            if (rank >= 10 && rank < 20) return "fm-status-strong";
            if (rank >= 40) return "fm-status-warn";
            
            return "";
        }

        private static string GetPlayingTimeClass(string playingTime)
        {
            if (string.IsNullOrEmpty(playingTime)) return "";
            var pt = playingTime.Trim();

            if (pt.Equals("Star Player", StringComparison.OrdinalIgnoreCase)) return "fm-status-gold";
            if (pt.Equals("Important Player", StringComparison.OrdinalIgnoreCase)) return "fm-status-elite";
            if (pt.Equals("Regular Starter", StringComparison.OrdinalIgnoreCase)) return "fm-status-strong";
            if (pt.Contains("Loan", StringComparison.OrdinalIgnoreCase)) return "fm-status-warn";

            return "";
        }

        private static string GetContractClass(string contractExpiry, DateTime? gameDate)
        {
            if (string.IsNullOrWhiteSpace(contractExpiry) || gameDate == null) return "";
            var contractDate = ParseSmartDate(contractExpiry);
            
            if (contractDate.HasValue)
            {
                var yearsRemaining = contractDate.Value.Year - gameDate.Value.Year;
                if (yearsRemaining <= 0) return "fm-status-critical";
                if (yearsRemaining == 1) return "fm-status-strong";
            }

            return "";
        }

        public static DateTime? ParseSmartDate(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;
            if (int.TryParse(input, out var year) && year > 1900 && year < 2100)
            {
                return new DateTime(year, 6, 30);
            }
            if (DateTime.TryParse(input, out var date))
            {
                return date;
            }
            return null;
        }

        private static int ParseCurrency(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return 0;
            var cleanString = new string(input.Where(c => char.IsDigit(c) || c == '-').ToArray());
            return int.TryParse(cleanString, out var value) ? value : 0;
        }
    }
}
