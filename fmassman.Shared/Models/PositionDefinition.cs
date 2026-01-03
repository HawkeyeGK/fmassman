using System;
using Newtonsoft.Json;

namespace fmassman.Shared.Models
{
    public class PositionDefinition
    {
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string ColorHex { get; set; } = "#0000FF";
    }
}
