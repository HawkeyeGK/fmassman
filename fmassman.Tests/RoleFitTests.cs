using fmassman.Shared;
using Xunit;
using System.Collections.Generic;

namespace fmassman.Tests
{
    public class RoleFitTests
    {
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
    }
}
