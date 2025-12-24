namespace fmassman.Shared
{
    public class RoleFitResult
    {
        public string RoleName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public double Score { get; set; } // 0-100, 1 decimal place
    }
}
