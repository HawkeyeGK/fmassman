using System.Collections.Generic;

namespace fmassman.Shared
{
    public class RoleDefinition
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Phase { get; set; } = string.Empty; // "InPossession" or "OutPossession"
        public Dictionary<string, double> Weights { get; set; } = new();
    }
}
