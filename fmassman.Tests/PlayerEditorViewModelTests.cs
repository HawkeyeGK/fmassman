using System.Text.Json;
using fmassman.Shared;
using fmassman.Web.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace fmassman.Tests;

public class PlayerEditorViewModelTests
{
    [Fact]
    public void Load_InitializesNullObjects()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var roster = new List<PlayerImportData>
        {
            new PlayerImportData { PlayerName = "Test Player", Snapshot = null } // Null snapshot
        };
        CreateTestRoster(tempFile, roster);

        var repo = new RosterRepository(tempFile);
        var nav = new TestNavigationManager();
        // Config no longer needed for VM

        var vm = new PlayerEditorViewModel(repo, nav);

        // Act
        vm.Load("Test Player");

        // Assert
        Assert.NotNull(vm.Player);
        Assert.NotNull(vm.Player.Snapshot);
        Assert.NotNull(vm.Player.Snapshot.Technical);
        Assert.NotNull(vm.Player.Snapshot.Mental);
        Assert.NotNull(vm.Player.Snapshot.Physical);
        Assert.NotNull(vm.Player.Snapshot.SetPieces);

        // Cleanup
        if (File.Exists(tempFile)) File.Delete(tempFile);
    }

    [Fact]
    public void Save_PersistsChanges()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var roster = new List<PlayerImportData>
        {
            new PlayerImportData 
            { 
                PlayerName = "Test Player", 
                Snapshot = new PlayerSnapshot 
                { 
                    Mental = new MentalAttributes { Determination = 5 } 
                } 
            }
        };
        CreateTestRoster(tempFile, roster);

        var repo = new RosterRepository(tempFile);
        var nav = new TestNavigationManager();
        // Config no longer needed for VM

        var vm = new PlayerEditorViewModel(repo, nav);
        vm.Load("Test Player");

        // Act
        vm.Player.Snapshot.Mental.Determination = 20;
        vm.Save();

        // Assert
        var json = File.ReadAllText(tempFile);
        var updatedRoster = JsonSerializer.Deserialize<List<PlayerImportData>>(json);
        var player = updatedRoster.First(p => p.PlayerName == "Test Player");
        Assert.Equal(20, player.Snapshot.Mental.Determination);
        Assert.Equal("/player/Test Player", nav.LastUri);

        // Cleanup
        if (File.Exists(tempFile)) File.Delete(tempFile);
    }

    private void CreateTestRoster(string path, List<PlayerImportData> roster)
    {
        var json = JsonSerializer.Serialize(roster);
        File.WriteAllText(path, json);
    }

    // Helper classes
    class TestNavigationManager : NavigationManager
    {
        public string LastUri { get; private set; }

        public TestNavigationManager()
        {
            Initialize("http://localhost/", "http://localhost/");
        }

        protected override void NavigateToCore(string uri, bool forceLoad)
        {
            LastUri = uri;
        }
    }

    class TestConfiguration : IConfiguration
    {
        private readonly Dictionary<string, string> _values = new();

        public string this[string key]
        {
            get => _values.TryGetValue(key, out var val) ? val : null;
            set => _values[key] = value;
        }

        public IConfigurationSection GetSection(string key) => throw new NotImplementedException();
        public IEnumerable<IConfigurationSection> GetChildren() => throw new NotImplementedException();
        public IChangeToken GetReloadToken() => throw new NotImplementedException();
    }
}
