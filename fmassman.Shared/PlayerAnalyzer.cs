using System;

namespace fmassman.Shared
{
    public class PlayerAnalyzer
    {
        public PlayerAnalysis Analyze(PlayerSnapshot? player)
        {
            if (player == null)
            {
                return new PlayerAnalysis();
            }

            // Ensure sub-objects are not null to avoid crashes, though normally they should be populated.
            // If they are null, we treat attributes as 0.
            var tech = player.Technical ?? new TechnicalAttributes();
            var mental = player.Mental ?? new MentalAttributes();
            var phys = player.Physical ?? new PhysicalAttributes();
            var setPieces = player.SetPieces ?? new SetPieceAttributes();

            var analysis = new PlayerAnalysis();

            // Speed: Pace, Acceleration
            analysis.Speed = CalculatePercentage(
                phys.Pace, phys.Acceleration
            );

            // DNA: Bravery, Composure, Concentration, Determination, Teamwork
            analysis.DNA = CalculatePercentage(
                mental.Bravery, mental.Composure, mental.Concentration, mental.Determination, mental.Teamwork
            );

            // AggressiveDefense: Tackling, Aggression, Bravery, WorkRate, Acceleration, Stamina
            analysis.AggressiveDefense = CalculatePercentage(
                tech.Tackling, mental.Aggression, mental.Bravery, mental.WorkRate, phys.Acceleration, phys.Stamina
            );

            // CautiousDefense: Positioning, Concentration, Anticipation, Decisions
            analysis.CautiousDefense = CalculatePercentage(
                mental.Positioning, mental.Concentration, mental.Anticipation, mental.Decisions
            );

            // DirectAttack: Crossing, Heading, Aggression, Bravery, WorkRate, Acceleration, Agility, Balance, JumpingReach, Pace, Stamina, Strength
            analysis.DirectAttack = CalculatePercentage(
                tech.Crossing, tech.Heading, mental.Aggression, mental.Bravery, mental.WorkRate,
                phys.Acceleration, phys.Agility, phys.Balance, phys.JumpingReach, phys.Pace, phys.Stamina, phys.Strength
            );

            // PossessionAttack: FirstTouch, Passing, Technique, Anticipation, Composure, Decisions, Flair, OffTheBall, Teamwork, Vision
            analysis.PossessionAttack = CalculatePercentage(
                tech.FirstTouch, tech.Passing, tech.Technique, mental.Anticipation, mental.Composure,
                mental.Decisions, mental.Flair, mental.OffTheBall, mental.Teamwork, mental.Vision
            );

            // Gegenpress (Weighted)
            // Group 1 (Weight 2.0): WorkRate, Stamina, Teamwork, Aggression, Anticipation
            double group1Sum = mental.WorkRate + phys.Stamina + mental.Teamwork + mental.Aggression + mental.Anticipation;
            
            // Group 2 (Weight 3.0): Acceleration, Pace, Decisions, Bravery, Tackling, Positioning
            double group2Sum = phys.Acceleration + phys.Pace + mental.Decisions + mental.Bravery + tech.Tackling + mental.Positioning;

            // Formula: ((SumGroup1 * 2) + (SumGroup2 * 3)) / 560 * 100
            double weightedSum = (group1Sum * 2) + (group2Sum * 3);
            analysis.Gegenpress = Math.Round((weightedSum / 560.0) * 100, 1);

            // Calculate Role Fits
            analysis.InPossessionFits = RoleFitCalculator.Calculate(player, "InPossession");
            analysis.OutPossessionFits = RoleFitCalculator.Calculate(player, "OutPossession");

            return analysis;
        }

        private double CalculatePercentage(params int[] attributes)
        {
            if (attributes == null || attributes.Length == 0)
                return 0;

            double sum = 0;
            foreach (var attr in attributes)
            {
                sum += attr;
            }

            // Max possible sum is Count * 20
            double max = attributes.Length * 20.0;
            
            return Math.Round((sum / max) * 100, 1);
        }
    }
}
