namespace SLSKDONET.Services.Ranking
{
    /// <summary>
    /// Phase 2.4: Strategy Pattern for configurable ranking modes.
    /// Allows users to switch between Quality First, DJ Mode, and Balanced strategies.
    /// </summary>
    public interface ISortingStrategy
    {
        string Name { get; }
        string Description { get; }
        
        /// <summary>
        /// Calculates the overall score for a track using this strategy's weights.
        /// </summary>
        double CalculateScore(
            double availabilityScore,
            double conditionsScore,
            double qualityScore,
            double musicalIntelligenceScore,
            double metadataScore,
            double stringMatchingScore,
            double tiebreakerScore,
            ScoringWeights weights
        );
    }
}
