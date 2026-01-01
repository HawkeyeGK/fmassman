using fmassman.Shared;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace fmassman.Tests
{
    public class PlayerAnalyzerTests
    {
        [Fact]
        public void Analyze_CalculatesSpeedCorrectly()
        {
            // Arrange
            var player = new PlayerSnapshot
            {
                Physical = new PhysicalAttributes { Pace = 10, Acceleration = 10 }
            };

            // Act
            var result = PlayerAnalyzer.Analyze(player);

            // Assert
            // (10 + 10) / (2 * 20) * 100 = 20 / 40 * 100 = 50
            Assert.Equal(50.0, result.Speed);
        }

        [Fact]
        public void Analyze_CalculatesGegenpressCorrectly()
        {
            // Arrange
            var player = new PlayerSnapshot
            {
                Mental = new MentalAttributes
                {
                    WorkRate = 10, Teamwork = 10, Aggression = 10, Anticipation = 10, // Group 1
                    Decisions = 10, Bravery = 10, Positioning = 10 // Group 2
                },
                Physical = new PhysicalAttributes
                {
                    Stamina = 10, // Group 1
                    Acceleration = 10, Pace = 10 // Group 2
                },
                Technical = new TechnicalAttributes
                {
                    Tackling = 10 // Group 2
                }
            };

            // Act
            var result = PlayerAnalyzer.Analyze(player);

            // Assert
            // Group 1 Sum: 10 * 5 = 50. Weighted: 50 * 2 = 100.
            // Group 2 Sum: 10 * 6 = 60. Weighted: 60 * 3 = 180.
            // Total Weighted: 280.
            // 280 / 560 * 100 = 0.5 * 100 = 50.
            Assert.Equal(50.0, result.Gegenpress);
        }
        
        [Fact]
        public void Analyze_HandlesNullSnapshot()
        {
            var result = PlayerAnalyzer.Analyze(null!);
            Assert.NotNull(result);
            Assert.Equal(0, result.Speed);
        }

        [Fact]
        public void Analyze_HandlesNullAttributes()
        {
            var player = new PlayerSnapshot(); // Attributes are null by default
            var result = PlayerAnalyzer.Analyze(player);
            Assert.NotNull(result);
            Assert.Equal(0, result.Speed);
        }
        [Fact]
        public void Analyze_CalculatesDNA()
        {
            // Arrange
            var player = new PlayerSnapshot
            {
                Mental = new MentalAttributes
                {
                    Bravery = 10, Composure = 10, Concentration = 10, Determination = 10, Teamwork = 10
                }
            };

            // Act
            var result = PlayerAnalyzer.Analyze(player);

            // Assert
            // 5 * 10 = 50. Max = 5 * 20 = 100. 50/100 = 50%.
            Assert.Equal(50.0, result.DNA);
        }

        [Fact]
        public void Analyze_CalculatesDirectAttack()
        {
            // Arrange
            var player = new PlayerSnapshot
            {
                Technical = new TechnicalAttributes { Crossing = 10, Heading = 10 },
                Mental = new MentalAttributes { Aggression = 10, Bravery = 10, WorkRate = 10 },
                Physical = new PhysicalAttributes 
                { 
                    Acceleration = 10, Agility = 10, Balance = 10, JumpingReach = 10, 
                    Pace = 10, Stamina = 10, Strength = 10 
                }
            };

            // Act
            var result = PlayerAnalyzer.Analyze(player);

            // Assert
            Assert.Equal(50.0, result.DirectAttack);
        }

        [Fact]
        public void Analyze_CalculatesPossessionAttack()
        {
            // Arrange
            var player = new PlayerSnapshot
            {
                Technical = new TechnicalAttributes { FirstTouch = 10, Passing = 10, Technique = 10 },
                Mental = new MentalAttributes 
                { 
                    Anticipation = 10, Composure = 10, Decisions = 10, Flair = 10, 
                    OffTheBall = 10, Teamwork = 10, Vision = 10 
                }
            };

            // Act
            var result = PlayerAnalyzer.Analyze(player);

            // Assert
            Assert.Equal(50.0, result.PossessionAttack);
        }

        [Fact]
        public void Analyze_CalculatesAggressiveDefense()
        {
            // Arrange: Tackling, Aggression, Bravery, WorkRate, Acceleration, Stamina
            var player = new PlayerSnapshot
            {
                Technical = new TechnicalAttributes { Tackling = 10 },
                Mental = new MentalAttributes { Aggression = 10, Bravery = 10, WorkRate = 10 },
                Physical = new PhysicalAttributes { Acceleration = 10, Stamina = 10 }
            };

            // Act
            var result = PlayerAnalyzer.Analyze(player);

            // Assert: 6 attrs * 10 = 60. Max = 6 * 20 = 120. 60/120 = 50%
            Assert.Equal(50.0, result.AggressiveDefense);
        }

        [Fact]
        public void Analyze_CalculatesCautiousDefense()
        {
            // Arrange: Positioning, Concentration, Anticipation, Decisions
            var player = new PlayerSnapshot
            {
                Mental = new MentalAttributes 
                { 
                    Positioning = 10, Concentration = 10, Anticipation = 10, Decisions = 10 
                }
            };

            // Act
            var result = PlayerAnalyzer.Analyze(player);

            // Assert: 4 attrs * 10 = 40. Max = 4 * 20 = 80. 40/80 = 50%
            Assert.Equal(50.0, result.CautiousDefense);
        }

        [Fact]
        public void Analyze_ReturnsZero_WhenAllAttributesAreZero()
        {
            // Arrange
            var player = new PlayerSnapshot
            {
                Technical = new TechnicalAttributes(),
                Mental = new MentalAttributes(),
                Physical = new PhysicalAttributes(),
                SetPieces = new SetPieceAttributes()
            };

            // Act
            var result = PlayerAnalyzer.Analyze(player);

            // Assert - all metrics should be 0, not NaN or negative
            Assert.Equal(0.0, result.Speed);
            Assert.Equal(0.0, result.DNA);
            Assert.Equal(0.0, result.AggressiveDefense);
            Assert.Equal(0.0, result.CautiousDefense);
            Assert.Equal(0.0, result.DirectAttack);
            Assert.Equal(0.0, result.PossessionAttack);
            Assert.Equal(0.0, result.Gegenpress);
        }

        [Fact]
        public void Analyze_GoalkeeperSnapshot_HandlesNullFieldAttributes()
        {
            // Arrange: A goalkeeper might only have Goalkeeping attributes populated
            var gkPlayer = new PlayerSnapshot
            {
                Technical = null,
                Mental = null,
                Physical = null,
                SetPieces = null,
                Goalkeeping = new GoalkeepingAttributes
                {
                    Reflexes = 18,
                    Handling = 17,
                    CommandOfArea = 16,
                    Communication = 15,
                    AerialReach = 14,
                    Kicking = 13,
                    OneOnOnes = 12,
                    Throwing = 11,
                    Punching = 10,
                    RushingOut = 9,
                    Eccentricity = 8,
                    FirstTouch = 7,
                    Passing = 6
                }
            };

            // Act - Should not throw
            var result = PlayerAnalyzer.Analyze(gkPlayer);

            // Assert - Field player metrics should be 0 (no crash)
            Assert.NotNull(result);
            Assert.Equal(0.0, result.Speed);
            Assert.Equal(0.0, result.DNA);
            Assert.Equal(0.0, result.AggressiveDefense);
            Assert.Equal(0.0, result.CautiousDefense);
            Assert.Equal(0.0, result.DirectAttack);
            Assert.Equal(0.0, result.PossessionAttack);
            Assert.Equal(0.0, result.Gegenpress);
        }

        [Fact]
        public void Analyze_GoalkeeperSnapshot_ReturnsRoleFitResults()
        {
            // Arrange: Setup GK roles in the calculator
            var gkRoles = new List<RoleDefinition>
            {
                new RoleDefinition 
                { 
                    Name = "Sweeper Keeper", 
                    Phase = "InPossession", 
                    Category = "Goalkeeper", 
                    Weights = new Dictionary<string, double> { { "Reflexes", 1.0 } } 
                },
                new RoleDefinition 
                { 
                    Name = "Traditional GK", 
                    Phase = "OutPossession", 
                    Category = "Goalkeeper", 
                    Weights = new Dictionary<string, double> { { "Handling", 1.0 } } 
                }
            };
            RoleFitCalculator.SetCache(gkRoles);

            var gkPlayer = new PlayerSnapshot
            {
                Goalkeeping = new GoalkeepingAttributes { Reflexes = 20, Handling = 20 }
            };

            // Act
            var result = PlayerAnalyzer.Analyze(gkPlayer);

            // Assert: Should have role fit results for goalkeeper
            Assert.NotNull(result.InPossessionFits);
            Assert.NotNull(result.OutPossessionFits);
            Assert.Single(result.InPossessionFits);
            Assert.Single(result.OutPossessionFits);
            Assert.Equal("Sweeper Keeper", result.InPossessionFits.First().RoleName);
            Assert.Equal("Traditional GK", result.OutPossessionFits.First().RoleName);
        }
    }
}
