using System.Collections.Generic;
using Newtonsoft.Json;

namespace fmassman.Shared
{
    public class RoleDefinition
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;
        
        [JsonProperty("pk")]
        public string PartitionKey { get; set; } = "RoleConfig";
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Phase { get; set; } = string.Empty; // "InPossession" or "OutPossession"
        public Dictionary<string, double> Weights { get; set; } = new();
    }
}
