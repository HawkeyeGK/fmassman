using System;
using System.Text.Json.Nodes;

namespace fmassman.Shared.Helpers
{
    public static class SafeJsonParser
    {
        // Helper to safely get a string value from JsonNode (handles both string and number types)
        public static string? GetSafeString(JsonNode? node)
        {
            if (node == null) return null;
            
            // Try to get the underlying JsonElement to check its type
            if (node is JsonValue jsonValue)
            {
                try
                {
                    // First try to get as string directly
                    return jsonValue.GetValue<string>();
                }
                catch (InvalidOperationException)
                {
                    // If it's not a string, convert to string representation
                    return node.ToJsonString().Trim('"');
                }
            }
            
            return node.ToString();
        }

        // Helper to safely get an int value from JsonNode (handles both int and string types)
        public static int GetSafeInt(JsonNode? node, int defaultValue = 0)
        {
            if (node == null) return defaultValue;
            
            if (node is JsonValue jsonValue)
            {
                try
                {
                    return jsonValue.GetValue<int>();
                }
                catch (InvalidOperationException)
                {
                    // Try parsing as string
                    var str = node.ToString();
                    return int.TryParse(str, out var result) ? result : defaultValue;
                }
            }
            
            return defaultValue;
        }
    }
}
