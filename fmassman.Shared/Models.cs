using System;
using Newtonsoft.Json;

namespace fmassman.Shared
{
    public class PlayerImportData
    {
        [JsonProperty("id")]
        public string PlayerName { get; set; } = string.Empty;
        public string DateOfBirth { get; set; } = string.Empty;
        public int HeightFeet { get; set; }
        public int HeightInches { get; set; }
        public List<string> TagIds { get; set; } = new List<string>();
        [JsonProperty("positionId")]
        public string? PositionId { get; set; }
        public PlayerSnapshot? Snapshot { get; set; }
        public PlayerAnalysis? Analysis { get; set; }
        public string? MiroWidgetId { get; set; }
    }

    public class PlayerSnapshot
    {
        public string SourceFilename { get; set; } = string.Empty;
        public string? RawImageBlobUrl { get; set; }
        public DateTime FileCreationDate { get; set; }
        public string GameDate { get; set; } = string.Empty;
        public string Personality { get; set; } = string.Empty;
        public string PlayingTime { get; set; } = string.Empty;
        public int Age { get; set; }
        public int TransferValueLow { get; set; }
        public int TransferValueHigh { get; set; }
        public string Wage { get; set; } = string.Empty;
        public string ContractExpiry { get; set; } = string.Empty;
        
        public TechnicalAttributes? Technical { get; set; }
        public SetPieceAttributes? SetPieces { get; set; }
        public MentalAttributes? Mental { get; set; }
        public PhysicalAttributes? Physical { get; set; }
        public GoalkeepingAttributes? Goalkeeping { get; set; }
    }

    public class TechnicalAttributes
    {
        public int Crossing { get; set; }
        public int Dribbling { get; set; }
        public int Finishing { get; set; }
        public int FirstTouch { get; set; }
        public int Heading { get; set; }
        public int LongShots { get; set; }
        public int Marking { get; set; }
        public int Passing { get; set; }
        public int Tackling { get; set; }
        public int Technique { get; set; }
    }

    public class SetPieceAttributes
    {
        public int Corners { get; set; }
        public int FreeKickTaking { get; set; }
        public int LongThrows { get; set; }
        public int PenaltyTaking { get; set; }
    }

    public class MentalAttributes
    {
        public int Aggression { get; set; }
        public int Anticipation { get; set; }
        public int Bravery { get; set; }
        public int Composure { get; set; }
        public int Concentration { get; set; }
        public int Decisions { get; set; }
        public int Determination { get; set; }
        public int Flair { get; set; }
        public int Leadership { get; set; }
        public int OffTheBall { get; set; }
        public int Positioning { get; set; }
        public int Teamwork { get; set; }
        public int Vision { get; set; }
        public int WorkRate { get; set; }
    }

    public class PhysicalAttributes
    {
        public int Acceleration { get; set; }
        public int Agility { get; set; }
        public int Balance { get; set; }
        public int JumpingReach { get; set; }
        public int NaturalFitness { get; set; }
        public int Pace { get; set; }
        public int Stamina { get; set; }
        public int Strength { get; set; }
    }

    public class GoalkeepingAttributes
    {
        public int AerialReach { get; set; }
        public int CommandOfArea { get; set; }
        public int Communication { get; set; }
        public int Eccentricity { get; set; }
        public int FirstTouch { get; set; }
        public int Handling { get; set; }
        public int Kicking { get; set; }
        public int OneOnOnes { get; set; }
        public int Passing { get; set; }
        public int Punching { get; set; }
        public int Reflexes { get; set; }
        public int RushingOut { get; set; }
        public int Throwing { get; set; }
    }
}
