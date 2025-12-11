using FM26_Helper.Shared;
using Xunit;

namespace FM26_Helper.Tests
{
    public class ScoutingConstantsTests
    {
        [Fact]
        public void GetPersonalityRank_ReturnsCorrectRank_ForKnownType()
        {
            Assert.Equal(1, ScoutingConstants.GetPersonalityRank("Model Citizen"));
            Assert.Equal(66, ScoutingConstants.GetPersonalityRank("Temperamental"));
        }

        [Fact]
        public void GetPersonalityRank_ReturnsDefault_ForUnknownOrEmpty()
        {
            Assert.Equal(50, ScoutingConstants.GetPersonalityRank("Unknown Type"));
            Assert.Equal(50, ScoutingConstants.GetPersonalityRank(""));
        }

        [Fact]
        public void GetPlayingTimeRank_ReturnsCorrectRank()
        {
            Assert.Equal(1, ScoutingConstants.GetPlayingTimeRank("Star Player"));
            Assert.Equal(8, ScoutingConstants.GetPlayingTimeRank("Surplus to Requirements"));
        }

        [Fact]
        public void GetPlayingTimeRank_ReturnsDefault_ForUnknown()
        {
            Assert.Equal(99, ScoutingConstants.GetPlayingTimeRank("Water Boy"));
        }
    }
}
