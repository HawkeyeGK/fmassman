using FM26_Helper.Shared;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace FM26_Helper.Tests
{
    public class RosterRepositoryTests
    {
        [Fact]
        public void SaveAndLoad_ShouldPersistData()
        {
            // Arrange
            string tempFile = Path.GetTempFileName();
            var repository = new RosterRepository();
            var players = new List<PlayerImportData>
            {
                new PlayerImportData { PlayerName = "Test Player 1", HeightFeet = 6 },
                new PlayerImportData { PlayerName = "Test Player 2", HeightFeet = 5 }
            };

            try
            {
                // Act
                repository.Save(tempFile, players);
                var loadedPlayers = repository.Load(tempFile);

                // Assert
                Assert.NotNull(loadedPlayers);
                Assert.Equal(2, loadedPlayers.Count);
                Assert.Equal("Test Player 1", loadedPlayers[0].PlayerName);
                Assert.Equal(6, loadedPlayers[0].HeightFeet);
                Assert.Equal("Test Player 2", loadedPlayers[1].PlayerName);
            }
            finally
            {
                // Cleanup
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        [Fact]
        public void Load_ShouldReturnEmptyList_WhenFileDoesNotExist()
        {
            // Arrange
            string nonExistentFile = "non_existent_file.json";
            var repository = new RosterRepository();

            // Act
            var result = repository.Load(nonExistentFile);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }
    }
}
