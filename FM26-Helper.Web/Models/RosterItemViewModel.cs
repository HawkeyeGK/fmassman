namespace FM26_Helper.Web.Models
{
    public class RosterItemViewModel
    {
        public string Name { get; set; } = string.Empty;
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
    }
}
