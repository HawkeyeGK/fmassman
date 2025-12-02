using FM26_Helper.Shared;
using Xunit;

namespace FM26_Helper.Tests
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
            var result = PlayerAnalyzer.Analyze(null);
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
    }
}
