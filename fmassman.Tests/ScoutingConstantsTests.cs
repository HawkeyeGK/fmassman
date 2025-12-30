using fmassman.Shared;
using Xunit;

namespace fmassman.Tests
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

        [Fact]
        public void GetCategoryRank_ReturnsCorrectIndex_ForKnownCategory()
        {
            Assert.Equal(0, ScoutingConstants.GetCategoryRank("Center Back"));
            Assert.Equal(4, ScoutingConstants.GetCategoryRank("Central Midfield"));
            Assert.Equal(8, ScoutingConstants.GetCategoryRank("Striker"));
        }

        [Fact]
        public void GetCategoryRank_ReturnsDefault_ForUnknownCategory()
        {
            Assert.Equal(99, ScoutingConstants.GetCategoryRank("Goalkeeper"));
            Assert.Equal(99, ScoutingConstants.GetCategoryRank(""));
        }

        [Fact]
        public void GetPersonalityRank_IsCaseInsensitive()
        {
            Assert.Equal(1, ScoutingConstants.GetPersonalityRank("model citizen"));
            Assert.Equal(1, ScoutingConstants.GetPersonalityRank("MODEL CITIZEN"));
        }

        [Fact]
        public void GetPlayingTimeRank_IsCaseInsensitive()
        {
            Assert.Equal(1, ScoutingConstants.GetPlayingTimeRank("star player"));
            Assert.Equal(1, ScoutingConstants.GetPlayingTimeRank("STAR PLAYER"));
        }
    }
}
