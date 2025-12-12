using Xunit;
using FM26_Helper.Web.Models;
using FM26_Helper.Shared.Services;
using FM26_Helper.Shared;
using System.IO;
using System.Collections.Generic;
using System.Text.Json;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace FM26_Helper.Tests
{
    public class RoleEditorViewModelTests : IDisposable
    {
        private readonly string _tempFilePath;
        private readonly RoleService _roleService;
        private readonly RoleEditorViewModel _viewModel;

        // Constructor runs before EACH test (Replaces [SetUp])
        public RoleEditorViewModelTests()
        {
            // 1. Create a temp file path
            _tempFilePath = Path.GetTempFileName();

            // 2. Seed it with valid JSON data
            var seedRoles = new List<RoleDefinition>
            {
                new RoleDefinition 
                { 
                    Name = "Test Role", 
                    Category = "Test Cat", 
                    Weights = new Dictionary<string, double> { { "Pace", 3.0 } } 
                }
            };
            File.WriteAllText(_tempFilePath, JsonSerializer.Serialize(seedRoles));

            // 3. Init Service and VM
            // Pass the temp file as BOTH baseline and local to ensure isolation
            _roleService = new RoleService(_tempFilePath, _tempFilePath);
            _viewModel = new RoleEditorViewModel(_roleService);
        }

        // Dispose runs after EACH test (Replaces [TearDown])
        public void Dispose()
        {
            if (File.Exists(_tempFilePath))
            {
                try { File.Delete(_tempFilePath); } catch { /* Ignore file lock issues during cleanup */ }
            }
        }

        [Fact]
        public void LoadData_ShouldPopulateRoles()
        {
            // Act
            _viewModel.LoadData();

            // Assert
            Assert.NotNull(_viewModel.Roles);
            Assert.Single(_viewModel.Roles);
            Assert.Equal("Test Role", _viewModel.Roles.First().Name);
        }

        [Fact]
        public void SelectRole_ShouldUpdateSelectionAndClearNewAttribute()
        {
            _viewModel.LoadData();
            var role = _viewModel.Roles.First();
            _viewModel.NewAttribute = "Pending Input";

            // Act
            _viewModel.SelectRole(role);

            // Assert
            Assert.Equal(role, _viewModel.SelectedRole);
            Assert.Equal(string.Empty, _viewModel.NewAttribute);
        }

        [Fact]
        public void UpdateWeight_ShouldModifyRoleData()
        {
            _viewModel.LoadData();
            var role = _viewModel.Roles.First();
            _viewModel.SelectRole(role);

            // Act
            _viewModel.UpdateWeight("Pace", 5.5);

            // Assert
            Assert.Equal(5.5, role.Weights["Pace"]);
        }

        [Fact]
        public void AddAttribute_ShouldAddToDictionary()
        {
            _viewModel.LoadData();
            var role = _viewModel.Roles.First();
            _viewModel.SelectRole(role);
            _viewModel.NewAttribute = "Stamina";

            // Act
            _viewModel.AddAttribute();

            // Assert
            Assert.True(role.Weights.ContainsKey("Stamina"));
            Assert.Equal(3.0, role.Weights["Stamina"]); // Default weight
            Assert.Equal(string.Empty, _viewModel.NewAttribute);
        }

        [Fact]
        public void RemoveWeight_ShouldRemoveFromDictionary()
        {
            _viewModel.LoadData();
            var role = _viewModel.Roles.First();
            _viewModel.SelectRole(role);

            // Act
            _viewModel.RemoveWeight("Pace");

            // Assert
            Assert.False(role.Weights.ContainsKey("Pace"));
        }
    }
}
