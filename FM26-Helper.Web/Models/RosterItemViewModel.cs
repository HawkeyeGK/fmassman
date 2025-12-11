namespace FM26_Helper.Web.Models
{
    public class RosterItemViewModel
    {
        public string Name { get; set; } = string.Empty;
        public string SortName { get; set; } = string.Empty;
        public int Age { get; set; }
        public DateTime? GameDate { get; set; }
        
        public string Personality { get; set; } = string.Empty;
        public string PlayingTime { get; set; } = string.Empty;
        
        public int TransferValueLow { get; set; }
        public int TransferValueHigh { get; set; }
        public string Wage { get; set; } = string.Empty;
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
    }
}
