using fmassman.Shared;
using Xunit;
using System.Collections.Generic;
using System.Linq;

namespace fmassman.Tests
{
    public class RoleFitTests
    {
        public RoleFitTests()
        {
            // Seed the static calculator with test roles before running tests
            RoleFitCalculator.SetCache(GetTestRoles());
        }

        private List<RoleDefinition> GetTestRoles()
        {
            return new List<RoleDefinition>
            {
                new RoleDefinition
                {
                    Name = "Test Role",
                    Category = "Test Category",
                    Phase = "InPossession", // Matches the test string
                    Weights = new Dictionary<string, double>
                    {
                        { "Finishing", 1.0 },
                        { "Pace", 1.0 },
                        { "Technique", 1.0 }
                    }
                }
            };
        }
        [Fact]
        public void Calculate_ShouldReturn100_WhenAllAttributesAre20()
        {
            // Arrange
            var player = CreatePlayerWithStats(20);

            // Act
            var results = RoleFitCalculator.Calculate(player, "InPossession");

            // Assert
            Assert.NotEmpty(results);
            foreach (var result in results)
            {
                // If a role has no weights, score is 0. Otherwise 100.
                // Most roles have weights.
                if (result.Score > 0)
                {
                    Assert.Equal(100.0, result.Score);
                }
            }
        }

        [Fact]
        public void Calculate_ShouldReturn50_WhenAllAttributesAre10()
        {
            // Arrange
            var player = CreatePlayerWithStats(10);

            // Act
            var results = RoleFitCalculator.Calculate(player, "InPossession");

            // Assert
            Assert.NotEmpty(results);
            foreach (var result in results)
            {
                if (result.Score > 0)
                {
                    Assert.Equal(50.0, result.Score);
                }
            }
        }

        private PlayerSnapshot CreatePlayerWithStats(int value)
        {
            return new PlayerSnapshot
            {
                Technical = new TechnicalAttributes
                {
                    Crossing = value, Dribbling = value, Finishing = value, FirstTouch = value,
                    Heading = value, LongShots = value, Marking = value, Passing = value,
                    Tackling = value, Technique = value
                },
                Mental = new MentalAttributes
                {
                    Aggression = value, Anticipation = value, Bravery = value, Composure = value,
                    Concentration = value, Decisions = value, Determination = value, Flair = value,
                    Leadership = value, OffTheBall = value, Positioning = value, Teamwork = value,
                    Vision = value, WorkRate = value
                },
                Physical = new PhysicalAttributes
                {
                    Acceleration = value, Agility = value, Balance = value, JumpingReach = value,
                    NaturalFitness = value, Pace = value, Stamina = value, Strength = value
                },
                SetPieces = new SetPieceAttributes
                {
                    Corners = value, FreeKickTaking = value, LongThrows = value, PenaltyTaking = value
                }
            };
        }

        [Fact]
        public void Calculate_ReturnsEmptyList_WhenPlayerIsNull()
        {
            var results = RoleFitCalculator.Calculate(null!, "InPossession");
            Assert.Empty(results);
        }

        [Fact]
        public void GetRoles_FiltersCorrectlyByPhase()
        {
            // Add an OutPossession role to the cache
            var roles = new List<RoleDefinition>
            {
                new RoleDefinition { Name = "InRole", Phase = "InPossession", Category = "Test", Weights = new Dictionary<string, double> { { "Pace", 1.0 } } },
                new RoleDefinition { Name = "OutRole", Phase = "OutPossession", Category = "Test", Weights = new Dictionary<string, double> { { "Tackling", 1.0 } } }
            };
            RoleFitCalculator.SetCache(roles);

            var inRoles = RoleFitCalculator.GetRoles("InPossession");
            var outRoles = RoleFitCalculator.GetRoles("OutPossession");

            Assert.Single(inRoles);
            Assert.Equal("InRole", inRoles.First().Name);
            Assert.Single(outRoles);
            Assert.Equal("OutRole", outRoles.First().Name);

            // Restore original test roles
            RoleFitCalculator.SetCache(GetTestRoles());
        }

        [Fact]
        public void Calculate_ReturnsZeroScore_WhenAllAttributesAreZero()
        {
            var player = CreatePlayerWithStats(0);
            var results = RoleFitCalculator.Calculate(player, "InPossession");

            Assert.NotEmpty(results);
            foreach (var result in results)
            {
                Assert.Equal(0.0, result.Score);
            }
        }
        [Fact]
        public void Calculate_ShouldFilterRolesByPlayerType()
        {
            // Arrange: Setup mixed roles
            var mixedRoles = new List<RoleDefinition>
            {
                new RoleDefinition { Name = "GkRole", Phase = "TestPhase", Category = "Goalkeeper", Weights = new Dictionary<string, double> { { "Reflexes", 1.0 } } },
                new RoleDefinition { Name = "FieldRole", Phase = "TestPhase", Category = "Forward", Weights = new Dictionary<string, double> { { "Finishing", 1.0 } } }
            };
            RoleFitCalculator.SetCache(mixedRoles);

            // Case 1: Goalkeeper Player (Has Goalkeeping attributes)
            var gkPlayer = new PlayerSnapshot
            {
                Goalkeeping = new GoalkeepingAttributes { Reflexes = 15 } // Not null
            };

            // Case 2: Field Player (Goalkeeping is null)
            var fieldPlayer = new PlayerSnapshot
            {
                Technical = new TechnicalAttributes { Finishing = 15 },
                Goalkeeping = null 
            };

            // Act
            var gkResults = RoleFitCalculator.Calculate(gkPlayer, "TestPhase");
            var fieldResults = RoleFitCalculator.Calculate(fieldPlayer, "TestPhase");

            // Assert
            
            // GK Player should ONLY get GkRole
            Assert.Single(gkResults); 
            Assert.Equal("GkRole", gkResults.First().RoleName);

            // Field Player should ONLY get FieldRole
            Assert.Single(fieldResults);
            Assert.Equal("FieldRole", fieldResults.First().RoleName);

            // Restore defaults
            RoleFitCalculator.SetCache(GetTestRoles());
        }
    }
}
