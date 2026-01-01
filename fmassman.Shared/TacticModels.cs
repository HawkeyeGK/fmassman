using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace fmassman.Shared
{
    public class Tactic
    {
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        public string Name { get; set; } = "";

        public List<string> InPossessionRoleIds { get; set; } = new();

        public List<string> OutPossessionRoleIds { get; set; } = new();
    }
}
