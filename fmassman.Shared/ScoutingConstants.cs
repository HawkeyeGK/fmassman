using System;
using System.Collections.Generic;

namespace fmassman.Shared
{
    public static class ScoutingConstants
    {
        // Custom Ranking: Elite -> Strong -> Acceptable -> Avoid -> Bad
        // Lower number = Better
        private static readonly Dictionary<string, int> _personalityRank = new(StringComparer.OrdinalIgnoreCase)
        {
            // Tier 1: Elite
            { "Model Citizen", 1 },
            { "Perfectionist", 2 },
            { "Resolute", 3 },
            { "Model Professional", 4 },
            { "Professional", 5 },

            // Tier 2: Strong
            { "Fairly Professional", 10 },
            { "Spirited", 11 },
            { "Resilient", 12 },

            // Tier 3: Acceptable
            { "Driven", 20 },
            { "Very Ambitious", 21 },
            { "Unsporting", 22 },
            { "Realist", 23 },
            { "Iron Willed", 24 },
            { "Born Leader", 25 },
            { "Mercenary", 26 },
            { "Fickle", 27 },
            { "Ambitious", 28 },
            { "Leader", 29 },
            { "Charismatic Leader", 30 },
            { "Fairly Loyal", 31 },
            { "Fairly Sporting", 32 },
            { "Balanced", 33 },

            // Tier 4: Avoid
            { "Sporting", 40 },
            { "Spineless", 41 },
            { "Low Self Belief", 42 },
            { "Honest", 43 },
            { "Light-Hearted", 44 },
            { "Fairly Ambitious", 45 },
            { "Fairly Determined", 46 },
            { "Determined", 47 },
            { "Very Loyal", 48 },
            { "Loyal", 49 },

            // Tier 5: Bad / Toxic
            { "Devoted", 60 },
            { "Low Determination", 61 },
            { "Easily Discouraged", 62 },
            { "Temperamental", 66 },
            { "Unambitious", 67 },
            { "Jovial", 68 },
            { "Casual", 69 },
            { "Slack", 70 }
        };

        private static readonly List<string> _categoryOrder = new()
        {
            "Center Back", "Full Back", "Wing Back", "Defensive Midfield", "Central Midfield",
            "Attacking Midfield", "Outside Midfield", "Wing", "Striker"
        };

        // Define the logical order for Playing Time (Lower number = Higher Status)
        private static readonly Dictionary<string, int> _playingTimeRank = new(StringComparer.OrdinalIgnoreCase)
        {
            { "Star Player", 1 },
            { "Important Player", 2 },
            { "Regular Starter", 3 },
            { "Squad Player", 4 },
            { "Impact Sub", 5 },
            { "Fringe Player", 6 },
            { "Youngster", 7 },
            { "Surplus to Requirements", 8 }
        };

        public static int GetPersonalityRank(string personality)
        {
            if (string.IsNullOrWhiteSpace(personality)) return 50; // Default
            return _personalityRank.TryGetValue(personality, out var rank) ? rank : 50;
        }

        public static int GetPlayingTimeRank(string playingTime)
        {
            if (string.IsNullOrWhiteSpace(playingTime)) return 99; // Default
            return _playingTimeRank.TryGetValue(playingTime, out var rank) ? rank : 99;
        }

        public static int GetCategoryRank(string category)
        {
            var index = _categoryOrder.IndexOf(category);
            return index == -1 ? 99 : index;
        }
    }
}
