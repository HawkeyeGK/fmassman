using System;
using Newtonsoft.Json;

namespace fmassman.Shared.Models
{
    public class MiroTokenSet
    {
        [JsonProperty("id")]
        public string Id => "miro_tokens";

        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public string Scope { get; set; } = string.Empty;
    }
}
