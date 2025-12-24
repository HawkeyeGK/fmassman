using Xunit;
using fmassman.Web.Models;
using fmassman.Shared;
using System;

namespace fmassman.Tests
{
    public class RosterItemViewModelTests
    {
        [Fact]
        public void FromPlayer_MapsDataCorrectly()
        {
            // Arrange
            var snapshot = new PlayerSnapshot
            {
                Age = 25,
                GameDate = "2025-01-01",
                Personality = "Model Citizen",
                PlayingTime = "Star Player",
                Technical = new TechnicalAttributes { Finishing = 15 },
                Physical = new PhysicalAttributes { Pace = 15, Acceleration = 15 } // Speed calculation: (15+15)*2.5 = 75
            };
            var data = new PlayerImportData
            {
                PlayerName = "John Doe",
                Snapshot = snapshot
            };

            // Act
            var viewModel = RosterItemViewModel.FromPlayer(data);

            // Assert
            Assert.Equal("John Doe", viewModel.Name);
            Assert.Equal("Doe, John", viewModel.SortName);
            Assert.Equal(25, viewModel.Age);
            Assert.Equal(75.0, viewModel.Speed);
        }

        [Fact]
        public void PersonalityCssClass_ReturnsCorrectStatus()
        {
            // Arrange
            var elite = new RosterItemViewModel { Personality = "Model Citizen" }; // Rank 1
            var strong = new RosterItemViewModel { Personality = "Resolute" }; // Rank 10-19 range usually
            // Slack isn't directly mapped in the simple Rank logic unless we check ScoutingConstants. 
            // Let's rely on the rank logic we know: < 10 Elite, 10-19 Strong, >= 40 Warn.
            // "Slack" is likely high rank (bad). "Unambitious" is definitely bad. 

            // Act
            var eliteClass = elite.PersonalityCssClass;

            // Assert
            Assert.Equal("fm-status-elite", eliteClass);
        }

        [Fact]
        public void PlayingTimeCssClass_ReturnsCorrectStatus()
        {
            // Arrange
            var gold = new RosterItemViewModel { PlayingTime = "Star Player" };
            var elite = new RosterItemViewModel { PlayingTime = "Important Player" };
            var strong = new RosterItemViewModel { PlayingTime = "Regular Starter" };
            var warn = new RosterItemViewModel { PlayingTime = "Loan" };

            // Act & Assert
            Assert.Equal("fm-status-gold", gold.PlayingTimeCssClass);
            Assert.Equal("fm-status-elite", elite.PlayingTimeCssClass);
            Assert.Equal("fm-status-strong", strong.PlayingTimeCssClass);
            Assert.Equal("fm-status-warn", warn.PlayingTimeCssClass);
        }

        [Fact]
        public void ContractCssClass_IdentifiesExpiringContracts()
        {
            // Arrange
            var gameDate = new DateTime(2025, 1, 1);
            
            var critical = new RosterItemViewModel 
            { 
                ContractExpiry = "2025-06-30", 
                GameDate = gameDate 
            };
            
            var strong = new RosterItemViewModel 
            { 
                ContractExpiry = "2026-06-30", 
                GameDate = gameDate 
            };

            var fine = new RosterItemViewModel
            {
                ContractExpiry = "2027-06-30",
                GameDate = gameDate
            };

            // Act & Assert
            Assert.Equal("fm-status-critical", critical.ContractCssClass); // Expires same year
            Assert.Equal("fm-status-strong", strong.ContractCssClass); // Expires next year
            Assert.Equal("", fine.ContractCssClass); // Expires later
        }

        [Fact]
        public void ParseSmartDate_HandlesFormats()
        {
            var date = RosterItemViewModel.ParseSmartDate("2026");
            Assert.Equal(new DateTime(2026, 6, 30), date);

            var fullDate = RosterItemViewModel.ParseSmartDate("1/1/2025");
            Assert.Equal(new DateTime(2025, 1, 1), fullDate);
        }
    }
}
