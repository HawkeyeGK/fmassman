namespace FM26_Helper.Web.Models
{
    public class RosterItemViewModel
    {
        public string Name { get; set; } = string.Empty;
        public string SortName { get; set; } = string.Empty;
        public int Age { get; set; }
        public string Wage { get; set; } = string.Empty;
        public string ContractExpiry { get; set; } = string.Empty;

        public double Speed { get; set; }
        public double DNA { get; set; }
        public double Gegenpress { get; set; }

        public string BestInPossessionRole { get; set; } = string.Empty;
        public double BestInPossessionScore { get; set; }
        
        public string BestOutPossessionRole { get; set; } = string.Empty;
        public double BestOutPossessionScore { get; set; }

        // Physical
        public int Pace { get; set; }
        public int Acceleration { get; set; }
        public int Stamina { get; set; }
        public int Strength { get; set; }

        // Technical
        public int Finishing { get; set; }
        public int Dribbling { get; set; }
        public int Passing { get; set; }
        public int Technique { get; set; }

        // Mental
        public int Composure { get; set; }
        public int Decisions { get; set; }
        public int Vision { get; set; }
        public int WorkRate { get; set; }
    }
}
