using Xunit;
using fmassman.Shared.Helpers;
using System.Text.Json.Nodes;

namespace fmassman.Tests
{
    public class SafeJsonParserTests
    {
        [Fact]
        public void GetSafeInt_ReturnsValue_WhenInputIsNumber()
        {
            var node = JsonNode.Parse("10");
            var result = SafeJsonParser.GetSafeInt(node);
            Assert.Equal(10, result);
        }

        [Fact]
        public void GetSafeInt_ParsesString_WhenInputIsStringNumber()
        {
            var node = JsonNode.Parse("\"10\"");
            var result = SafeJsonParser.GetSafeInt(node);
            Assert.Equal(10, result);
        }

        [Fact]
        public void GetSafeInt_ReturnsDefault_WhenInputIsGarbage()
        {
            var node = JsonNode.Parse("\"abc\"");
            var result = SafeJsonParser.GetSafeInt(node);
            Assert.Equal(0, result);
        }

        [Fact]
        public void GetSafeInt_ReturnsDefault_WhenInputIsNull()
        {
            var result = SafeJsonParser.GetSafeInt(null);
            Assert.Equal(0, result);
        }

        [Fact]
        public void GetSafeString_ReturnsString_WhenInputIsString()
        {
            var node = JsonNode.Parse("\"Hello\"");
            var result = SafeJsonParser.GetSafeString(node);
            Assert.Equal("Hello", result);
        }

        [Fact]
        public void GetSafeString_ConvertsNumber_WhenInputIsNumber()
        {
            var node = JsonNode.Parse("1995");
            var result = SafeJsonParser.GetSafeString(node);
            Assert.Equal("1995", result);
        }

        [Fact]
        public void GetSafeInt_ReturnsCustomDefault_WhenInputIsNull()
        {
            var result = SafeJsonParser.GetSafeInt(null, -1);
            Assert.Equal(-1, result);
        }

        [Fact]
        public void GetSafeInt_ReturnsCustomDefault_WhenInputIsGarbage()
        {
            var node = JsonNode.Parse("\"not a number\"");
            var result = SafeJsonParser.GetSafeInt(node, 42);
            Assert.Equal(42, result);
        }

        [Fact]
        public void GetSafeString_ReturnsNull_WhenInputIsNull()
        {
            var result = SafeJsonParser.GetSafeString(null);
            Assert.Null(result);
        }

        [Fact]
        public void GetSafeString_HandlesEmptyString()
        {
            var node = JsonNode.Parse("\"\"");
            var result = SafeJsonParser.GetSafeString(node);
            Assert.Equal("", result);
        }

        [Fact]
        public void GetSafeInt_HandlesNegativeNumber()
        {
            var node = JsonNode.Parse("-5");
            var result = SafeJsonParser.GetSafeInt(node);
            Assert.Equal(-5, result);
        }

        [Fact]
        public void GetSafeInt_HandlesNegativeStringNumber()
        {
            var node = JsonNode.Parse("\"-10\"");
            var result = SafeJsonParser.GetSafeInt(node);
            Assert.Equal(-10, result);
        }
    }
}
