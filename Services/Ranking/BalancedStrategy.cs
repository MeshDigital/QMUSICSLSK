namespace SLSKDONET.Services.Ranking
{
    /// <summary>
    /// Balanced strategy: Equal weight to quality and musical intelligence.
    /// This is the default ORBIT ranking mode.
    /// </summary>
    public class BalancedStrategy : ISortingStrategy
    {
        public string Name => "Balanced";
        public string Description => "Equal weight to quality and musical intelligence. Default mode.";
        
        public double CalculateScore(
            double availabilityScore,
            double conditionsScore,
            double qualityScore,
            double musicalIntelligenceScore,
            double metadataScore,
            double stringMatchingScore,
            double tiebreakerScore,
            ScoringWeights weights)
        {
            return (availabilityScore * weights.AvailabilityWeight)
                 + (conditionsScore * weights.ConditionsWeight)
                 + (qualityScore * weights.QualityWeight)
                 + (musicalIntelligenceScore * weights.MusicalWeight)
                 + (metadataScore * weights.MetadataWeight)
                 + (stringMatchingScore * weights.StringWeight)
                 + tiebreakerScore;
        }
    }
}
