using System.Collections.Generic;
using fmassman.Shared;
using Newtonsoft.Json;
using Xunit;

namespace fmassman.Tests
{
    public class PlayerImportDataTests
    {
        [Fact]
        public void Serialization_PreservesTagIds()
        {
            // Arrange
            var player = new PlayerImportData
            {
                PlayerName = "Test Player",
                TagIds = new List<string> { "tag1", "tag2" }
            };

            // Act
            var json = JsonConvert.SerializeObject(player);
            var deserialized = JsonConvert.DeserializeObject<PlayerImportData>(json);

            // Assert
            Assert.NotNull(deserialized);
            Assert.Equal(player.PlayerName, deserialized.PlayerName);
            Assert.NotNull(deserialized.TagIds);
            Assert.Equal(2, deserialized.TagIds.Count);
            Assert.Contains("tag1", deserialized.TagIds);
            Assert.Contains("tag2", deserialized.TagIds);
        }

        [Fact]
        public void TagIds_DefaultsToEmptyList_WhenMissingInJson()
        {
            // Arrange
            var json = "{\"id\": \"Test Player\"}"; // Missing TagIds

            // Act
            var deserialized = JsonConvert.DeserializeObject<PlayerImportData>(json);

            // Assert
            Assert.NotNull(deserialized);
            Assert.NotNull(deserialized.TagIds);
            Assert.Empty(deserialized.TagIds);
        }
    }
}
