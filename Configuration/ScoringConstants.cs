namespace SLSKDONET.Configuration;

/// <summary>
/// Scoring constants for search result ranking.
/// Based on proven patterns from slsk-batchdl with Antigravity enhancements.
/// </summary>
public static class ScoringConstants
{
    // ===== Quality Tiers (Primary Scoring) =====
    
    /// <summary>
    /// Base score for lossless formats (FLAC, WAV, ALAC, APE)
    /// </summary>
    public const int LosslessBase = 450;
    
    /// <summary>
    /// Base score for high-quality lossy (320kbps MP3, 256kbps AAC)
    /// </summary>
    public const int HighQualityBase = 300;
    
    /// <summary>
    /// Base score for medium-quality lossy (192-256kbps)
    /// </summary>
    public const int MediumQualityBase = 150;
    
    /// <summary>
    /// Bonus for high sample rate lossless (96kHz+)
    /// </summary>
    public const int HighSampleRateBonus = 25;
    
    // ===== Musical Intelligence (Tiebreaker) =====
    
    /// <summary>
    /// Bonus for exact BPM match in filename
    /// </summary>
    public const int BpmMatchBonus = 100;
    
    /// <summary>
    /// Bonus for BPM found in directory path (slightly lower confidence)
    /// </summary>
    public const int PathBpmBonus = 75;
    
    /// <summary>
    /// Bonus for exact musical key match
    /// </summary>
    public const int KeyMatchBonus = 75;
    
    /// <summary>
    /// Bonus for harmonic key compatibility (Camelot wheel)
    /// </summary>
    public const int HarmonicKeyBonus = 50;
    
    // ===== Duration & Validation =====
    
    /// <summary>
    /// Duration tolerance in seconds for strict gating (Phase 0.4)
    /// </summary>
    public const int DurationToleranceSeconds = 30;
    
    /// <summary>
    /// Smart duration tolerance for version matching
    /// </summary>
    public const int SmartDurationToleranceSeconds = 15;
    
    /// <summary>
    /// Minimum bytes per second for filesize validation (~64kbps)
    /// </summary>
    public const int MinBytesPerSecond = 8000;
    
    /// <summary>
    /// Filesize threshold for suspicion (50% of expected)
    /// </summary>
    public const double FilesizeSuspicionThreshold = 0.5;
    
    /// <summary>
    /// VBR/CBR validation threshold (80% of expected size)
    /// From slsk-batchdl: catches fake upconverted files
    /// </summary>
    public const double VbrValidationThreshold = 0.8;
    
    // ===== Uploader Trust =====
    
    /// <summary>
    /// Bonus for free upload slot
    /// </summary>
    public const int FreeSlotBonus = 2000;
    
    /// <summary>
    /// Penalty per queue position
    /// </summary>
    public const int QueuePenaltyPerItem = 10;
    
    /// <summary>
    /// Heavy penalty for very long queues (>50 items)
    /// </summary>
    public const int LongQueuePenalty = 500;
    
    /// <summary>
    /// Queue length threshold for heavy penalty
    /// </summary>
    public const int LongQueueThreshold = 50;
    
    // ===== String Matching =====
    
    /// <summary>
    /// Weight for title similarity
    /// </summary>
    public const int TitleSimilarityWeight = 200;
    
    /// <summary>
    /// Weight for artist similarity
    /// </summary>
    public const int ArtistSimilarityWeight = 100;
    
    /// <summary>
    /// Weight for album similarity
    /// </summary>
    public const int AlbumSimilarityWeight = 50;
    
    // ===== Path-Based Search =====
    
    /// <summary>
    /// Decay factor for tokens found in parent directory
    /// </summary>
    public const double PathDecayFactor = 0.9;
    
    /// <summary>
    /// Decay factor for tokens found 2+ levels up
    /// </summary>
    public const double DeepPathDecayFactor = 0.7;
}
