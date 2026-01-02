using System;
using Newtonsoft.Json;

namespace fmassman.Shared
{
    public class TagDefinition
    {
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public bool IsRostered { get; set; } = true;
        public bool IsDefault { get; set; }
        public bool IsArchived { get; set; }
    }
}
