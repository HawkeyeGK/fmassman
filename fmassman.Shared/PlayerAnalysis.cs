namespace fmassman.Shared
{
    public class PlayerAnalysis
    {
        public double Speed { get; set; }
        public double DNA { get; set; }
        public double AggressiveDefense { get; set; }
        public double CautiousDefense { get; set; }
        public double DirectAttack { get; set; }
        public double PossessionAttack { get; set; }
        public double Gegenpress { get; set; }

        public List<RoleFitResult> InPossessionFits { get; set; } = new();
        public List<RoleFitResult> OutPossessionFits { get; set; } = new();
    }
}
