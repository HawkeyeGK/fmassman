using fmassman.Shared;
using Xunit;
using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace fmassman.Tests
{
    public class TacticTests
    {
        [Fact]
        public void Tactic_ShouldHaveDefaultValues_WhenCreated()
        {
            // Act
            var tactic = new Tactic();

            // Assert
            Assert.NotNull(tactic.Id);
            Assert.NotEmpty(tactic.Id);
            Assert.Equal("", tactic.Name);
            Assert.NotNull(tactic.InPossessionRoleIds);
            Assert.Empty(tactic.InPossessionRoleIds);
            Assert.NotNull(tactic.OutPossessionRoleIds);
            Assert.Empty(tactic.OutPossessionRoleIds);
        }

        [Fact]
        public void Tactic_Id_ShouldBeUnique_ForMultipleInstances()
        {
            // Act
            var tactic1 = new Tactic();
            var tactic2 = new Tactic();

            // Assert
            Assert.NotEqual(tactic1.Id, tactic2.Id);
        }

        [Fact]
        public void Tactic_ShouldSerializeCorrectly_WithJsonProperty()
        {
            // Arrange
            var tactic = new Tactic
            {
                Id = "test-id-123",
                Name = "4-3-3 Attack",
                InPossessionRoleIds = new List<string> { "role1", "role2" },
                OutPossessionRoleIds = new List<string> { "role3", "role4" }
            };

            // Act
            var json = JsonConvert.SerializeObject(tactic);

            // Assert
            Assert.Contains("\"id\":\"test-id-123\"", json);
            Assert.Contains("\"Name\":\"4-3-3 Attack\"", json);
            Assert.Contains("\"InPossessionRoleIds\":[\"role1\",\"role2\"]", json);
        }

        [Fact]
        public void Tactic_ShouldDeserializeCorrectly_FromJson()
        {
            // Arrange
            var json = @"{
                ""id"": ""test-id-456"",
                ""Name"": ""5-4-1 Defensive"",
                ""InPossessionRoleIds"": [""role1""],
                ""OutPossessionRoleIds"": [""role2"", ""role3""]
            }";

            // Act
            var tactic = JsonConvert.DeserializeObject<Tactic>(json);

            // Assert
            Assert.NotNull(tactic);
            Assert.Equal("test-id-456", tactic.Id);
            Assert.Equal("5-4-1 Defensive", tactic.Name);
            Assert.Single(tactic.InPossessionRoleIds);
            Assert.Equal("role1", tactic.InPossessionRoleIds[0]);
            Assert.Equal(2, tactic.OutPossessionRoleIds.Count);
        }

        [Fact]
        public void Tactic_ShouldHandleEmptyRoleIds_WhenDeserializing()
        {
            // Arrange
            var json = @"{
                ""id"": ""test-id-789"",
                ""Name"": ""Empty Tactic""
            }";

            // Act
            var tactic = JsonConvert.DeserializeObject<Tactic>(json);

            // Assert
            Assert.NotNull(tactic);
            Assert.NotNull(tactic.InPossessionRoleIds);
            Assert.NotNull(tactic.OutPossessionRoleIds);
            // Note: Default values won't be applied during deserialization
            // The deserializer will leave them as null if not present in JSON
        }

        [Fact]
        public void Tactic_Name_ShouldBeSettable()
        {
            // Arrange
            var tactic = new Tactic();
            var expectedName = "Custom Formation";

            // Act
            tactic.Name = expectedName;

            // Assert
            Assert.Equal(expectedName, tactic.Name);
        }

        [Fact]
        public void Tactic_RoleIds_ShouldBeModifiable()
        {
            // Arrange
            var tactic = new Tactic();

            // Act
            tactic.InPossessionRoleIds.Add("new-role-1");
            tactic.InPossessionRoleIds.Add("new-role-2");
            tactic.OutPossessionRoleIds.Add("defensive-role-1");

            // Assert
            Assert.Equal(2, tactic.InPossessionRoleIds.Count);
            Assert.Single(tactic.OutPossessionRoleIds);
            Assert.Contains("new-role-1", tactic.InPossessionRoleIds);
        }

        [Fact]
        public void Tactic_ShouldValidateGuidFormat_ForId()
        {
            // Arrange & Act
            var tactic = new Tactic();

            // Assert
            Assert.True(Guid.TryParse(tactic.Id, out _), "Default ID should be a valid GUID");
        }
    }
}
