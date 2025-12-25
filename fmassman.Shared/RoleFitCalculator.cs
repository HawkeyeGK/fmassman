using System;
using System.Collections.Generic;

using System.Linq;
using System.Text.Json;

namespace fmassman.Shared
{
    public static class RoleFitCalculator
    {
        private static List<RoleDefinition> _cachedRoles = new();
        
        // The "Magic Map" - replaces slow Reflection with instant lookups
        private static readonly Dictionary<string, Func<PlayerSnapshot, int>> _attributeMap = new(StringComparer.OrdinalIgnoreCase);

        // Expose valid attributes for the Editor UI
        public static IEnumerable<string> ValidAttributeNames => _attributeMap.Keys.OrderBy(k => k);

        // Allow the Service to update the engine at runtime
        public static void SetCache(List<RoleDefinition> roles)
        {
            _cachedRoles = roles;
        }
        
        // Expose roles for UI generation when no players exist
        public static IEnumerable<RoleDefinition> GetRoles(string phase)
        {
            return _cachedRoles.Where(r => r.Phase.Equals(phase, StringComparison.OrdinalIgnoreCase));
        }

        static RoleFitCalculator()
        {
            _cachedRoles ??= new List<RoleDefinition>();
            InitializeAttributeMap();
        }

        private static void InitializeAttributeMap()
        {
            // --- Technical ---
            _attributeMap["Crossing"] = p => p.Technical?.Crossing ?? 0;
            _attributeMap["Dribbling"] = p => p.Technical?.Dribbling ?? 0;
            _attributeMap["Finishing"] = p => p.Technical?.Finishing ?? 0;
            _attributeMap["FirstTouch"] = p => p.Technical?.FirstTouch ?? 0;
            _attributeMap["Heading"] = p => p.Technical?.Heading ?? 0;
            _attributeMap["LongShots"] = p => p.Technical?.LongShots ?? 0;
            _attributeMap["Marking"] = p => p.Technical?.Marking ?? 0;
            _attributeMap["Passing"] = p => p.Technical?.Passing ?? 0;
            _attributeMap["Tackling"] = p => p.Technical?.Tackling ?? 0;
            _attributeMap["Technique"] = p => p.Technical?.Technique ?? 0;

            // --- Set Pieces ---
            _attributeMap["Corners"] = p => p.SetPieces?.Corners ?? 0;
            _attributeMap["FreeKickTaking"] = p => p.SetPieces?.FreeKickTaking ?? 0;
            _attributeMap["LongThrows"] = p => p.SetPieces?.LongThrows ?? 0;
            _attributeMap["PenaltyTaking"] = p => p.SetPieces?.PenaltyTaking ?? 0;

            // --- Mental ---
            _attributeMap["Aggression"] = p => p.Mental?.Aggression ?? 0;
            _attributeMap["Anticipation"] = p => p.Mental?.Anticipation ?? 0;
            _attributeMap["Bravery"] = p => p.Mental?.Bravery ?? 0;
            _attributeMap["Composure"] = p => p.Mental?.Composure ?? 0;
            _attributeMap["Concentration"] = p => p.Mental?.Concentration ?? 0;
            _attributeMap["Decisions"] = p => p.Mental?.Decisions ?? 0;
            _attributeMap["Determination"] = p => p.Mental?.Determination ?? 0;
            _attributeMap["Flair"] = p => p.Mental?.Flair ?? 0;
            _attributeMap["Leadership"] = p => p.Mental?.Leadership ?? 0;
            _attributeMap["OffTheBall"] = p => p.Mental?.OffTheBall ?? 0;
            _attributeMap["Positioning"] = p => p.Mental?.Positioning ?? 0;
            _attributeMap["Teamwork"] = p => p.Mental?.Teamwork ?? 0;
            _attributeMap["Vision"] = p => p.Mental?.Vision ?? 0;
            _attributeMap["WorkRate"] = p => p.Mental?.WorkRate ?? 0;

            // --- Physical ---
            _attributeMap["Acceleration"] = p => p.Physical?.Acceleration ?? 0;
            _attributeMap["Agility"] = p => p.Physical?.Agility ?? 0;
            _attributeMap["Balance"] = p => p.Physical?.Balance ?? 0;
            _attributeMap["JumpingReach"] = p => p.Physical?.JumpingReach ?? 0;
            _attributeMap["NaturalFitness"] = p => p.Physical?.NaturalFitness ?? 0;
            _attributeMap["Pace"] = p => p.Physical?.Pace ?? 0;
            _attributeMap["Stamina"] = p => p.Physical?.Stamina ?? 0;
            _attributeMap["Strength"] = p => p.Physical?.Strength ?? 0;
        }

        public static List<RoleFitResult> Calculate(PlayerSnapshot player, string phase)
        {
            if (player == null) return new List<RoleFitResult>();

            var results = new List<RoleFitResult>();
            var roles = _cachedRoles.Where(r => r.Phase.Equals(phase, StringComparison.OrdinalIgnoreCase));

            foreach (var role in roles)
            {
                double totalWeightedScore = 0;
                double maxPossible = 0;

                foreach (var weight in role.Weights)
                {
                    var attributeName = weight.Key;
                    var weightValue = weight.Value;

                    // O(1) Lookup instead of Reflection
                    var attributeValue = GetAttributeValue(player, attributeName);

                    totalWeightedScore += attributeValue * weightValue;
                    maxPossible += 20 * weightValue;
                }

                double score = 0;
                if (maxPossible > 0)
                {
                    score = Math.Round((totalWeightedScore / maxPossible) * 100, 1);
                }

                results.Add(new RoleFitResult
                {
                    RoleName = role.Name,
                    Category = role.Category,
                    Score = score
                });
            }

            return results.OrderByDescending(r => r.Score).ToList();
        }

        private static int GetAttributeValue(PlayerSnapshot player, string attributeName)
        {
            if (_attributeMap.TryGetValue(attributeName, out var selector))
            {
                return selector(player);
            }
            // If the attribute name in JSON doesn't match anything in our map, return 0 (safe fallback)
            return 0;
        }
    }
}
