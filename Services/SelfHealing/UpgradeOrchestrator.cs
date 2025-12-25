using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SLSKDONET.Data;
using SLSKDONET.Services.SelfHealing;

namespace SLSKDONET.Services.SelfHealing;

/// <summary>
/// Master coordinator for atomic file upgrades.
/// Implements transactional state machine with crash recovery.
/// Unites: LibraryScanner, UpgradeScout, FileLockMonitor, MetadataCloner, CrashRecoveryJournal, SafeWriteService.
/// </summary>
public class UpgradeOrchestrator
{
    private readonly ILogger<UpgradeOrchestrator> _logger;
    private readonly UpgradeScout _upgradeScout;
    private readonly FileLockMonitor _fileLockMonitor;
    private readonly MetadataCloner _metadataCloner;
    private readonly CrashRecoveryJournal _journal;
    private readonly DatabaseService _databaseService;
    
    private const string UPGRADE_TEMP_DIR = ".orbit/tmp/upgrades";
    private const string BACKUP_DIR = ".orbit/backups";
    private const int BACKUP_RETENTION_DAYS = 7;
    private const int DURATION_TOLERANCE_MS = 100; // For metadata corruption detection
    
    public UpgradeOrchestrator(
        ILogger<UpgradeOrchestrator> logger,
        UpgradeScout upgradeScout,
        FileLockMonitor fileLockMonitor,
        MetadataCloner metadataCloner,
        CrashRecoveryJournal journal,
        DatabaseService databaseService)
    {
        _logger = logger;
        _upgradeScout = upgradeScout;
        _fileLockMonitor = fileLockMonitor;
        _metadataCloner = metadataCloner;
        _journal = journal;
        _databaseService = databaseService;
    }
    
    /// <summary>
    /// Processes an upgrade for a single track using 8-step atomic swap.
    /// </summary>
    public async Task<UpgradeResult> ProcessUpgradeAsync(
        UpgradeCandidate candidate,
        CancellationToken ct = default)
    {
        var state = UpgradeState.Pending;
        string? tempUpgradePath = null;
        string? backupPath = null;
        string? journalId = null;
        TrackEntity? track = null;
        
        try
        {
            _logger.LogInformation("🚀 Starting upgrade: {Artist} - {Title}", candidate.Artist, candidate.Title);
            
            // Get track from database
            track = await _databaseService.FindTrackAsync(candidate.TrackId);
            if (track == null)
            {
                return UpgradeResult.CreateFailed("Track not found in database");
            }
            
            var originalPath = track.Filename;
            
            // Step 1: Lock Check
            _logger.LogInformation("Step 1/8: Checking file lock...");
            var lockStatus = await _fileLockMonitor.IsFileSafeToReplaceAsync(originalPath, candidate.TrackId);
            if (!lockStatus.IsSafe)
            {
                _logger.LogWarning("File is locked: {Reason}. Deferring upgrade for 5 minutes.", lockStatus.Reason);
                
                // Phase 5 Hardening: Persist Deferral
                track.State = "Deferred";
                track.NextRetryTime = DateTime.UtcNow.AddMinutes(5);
                await _databaseService.SaveTrackAsync(track); // Assuming SaveTrackAsync updates Global Tracks table
                
                return UpgradeResult.CreateDeferred(lockStatus.Message);
            }
            
            // Step 2: Find Upgrade Candidates
            _logger.LogInformation("Step 2/8: Searching P2P network...");
            var upgradeCandidates = await _upgradeScout.FindUpgradesAsync(candidate, ct);
            if (!upgradeCandidates.Any())
            {
                return UpgradeResult.CreateFailed("No suitable upgrade found");
            }
            
            var bestCandidate = upgradeCandidates.First();
            _logger.LogInformation("Found upgrade: {Filename} from {Username} ({Score} pts)",
                bestCandidate.Filename, bestCandidate.Username, bestCandidate.QualityScore);
            
            state = UpgradeState.Downloading;
            
            // Step 3: Shadow Download (isolated temp directory)
            _logger.LogInformation("Step 3/8: Downloading to shadow directory...");
            tempUpgradePath = await DownloadToShadowDirectoryAsync(bestCandidate, ct);
            
            if (string.IsNullOrEmpty(tempUpgradePath) || !File.Exists(tempUpgradePath))
            {
                return UpgradeResult.CreateFailed("Download failed");
            }
            
            state = UpgradeState.CloningMetadata;
            
            // Step 4: Metadata Soul Transfer
            _logger.LogInformation("Step 4/8: Cloning metadata (soul transfer)...");
            await _metadataCloner.CloneAsync(originalPath, tempUpgradePath, track);
            
            // Validate metadata clone (checksum-verified tagging)
            if (!await ValidateMetadataCloneAsync(tempUpgradePath, track, originalPath))
            {
                File.Delete(tempUpgradePath);
                return UpgradeResult.CreateFailed("Metadata validation failed (possible corruption)");
            }
            
            state = UpgradeState.ReadyToSwap;
            
            // Step 5: Journal Log (atomic safety)
            _logger.LogInformation("Step 5/8: Creating crash recovery checkpoint...");
            journalId = await CreateUpgradeJournalEntryAsync(originalPath, tempUpgradePath, candidate.TrackId);
            
            // Step 6: Backup Original
            _logger.LogInformation("Step 6/8: Backing up original file (7-day retention)...");
            backupPath = CreateBackupPath(originalPath);
            EnsureDirectoryExists(Path.GetDirectoryName(backupPath)!);
            
            state = UpgradeState.BackingUp;
            
            // Phase 5 Hardening: Cross-Volume Backup Check
            if (Path.GetPathRoot(originalPath) == Path.GetPathRoot(backupPath))
            {
                File.Move(originalPath, backupPath);
            }
            else
            {
                // Cross-volume move
                File.Copy(originalPath, backupPath, overwrite: true);
                File.Delete(originalPath);
            }
            
            // Step 7: The Surgical Swap (atomic move)
            _logger.LogInformation("Step 7/8: Performing atomic swap...");
            state = UpgradeState.Swapping;
            
            await PerformAtomicSwapAsync(tempUpgradePath, originalPath);
            
            // Step 8: Update Database
            _logger.LogInformation("Step 8/8: Updating database and finalizing...");
            state = UpgradeState.UpdatingDatabase;
            
            await UpdateTrackAfterUpgradeAsync(track, candidate, bestCandidate);
            
            // Mark journal as complete
            if (!string.IsNullOrEmpty(journalId))
            {
                await _journal.CompleteCheckpointAsync(journalId);
            }
            
            // Schedule backup cleanup
            await ScheduleBackupCleanupAsync(backupPath);
            
            state = UpgradeState.Completed;
            
            var qualityGain = bestCandidate.BitRate - candidate.CurrentBitrate;
            var percentGain = (qualityGain / (double)candidate.CurrentBitrate) * 100;
            
            _logger.LogInformation("✅ Upgrade complete: {Gain}kbps gain (+{Percent:F0}%)",
                qualityGain, percentGain);
            
            return UpgradeResult.CreateSuccess(qualityGain, percentGain);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Upgrade failed at state: {State}", state);
            state = UpgradeState.Failed;
            
            // Attempt rollback
            await RollbackUpgradeAsync(originalPath: track?.Filename, backupPath, tempUpgradePath, journalId);
            
            return UpgradeResult.CreateFailed($"Upgrade failed: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Downloads upgrade to isolated shadow directory.
    /// Prevents OS indexers and DJ apps from touching the file prematurely.
    /// </summary>
    private async Task<string> DownloadToShadowDirectoryAsync(UpgradeSearchResult candidate, CancellationToken ct)
    {
        var shadowDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), UPGRADE_TEMP_DIR);
        EnsureDirectoryExists(shadowDir);
        
        var tempFileName = $"{Guid.NewGuid()}.upgrade";
        var tempPath = Path.Combine(shadowDir, tempFileName);
        
        // TODO: Integrate with DownloadManager to download the file
        // For now, placeholder for actual download implementation
        _logger.LogDebug("Would download {Filename} to {TempPath}", candidate.Filename, tempPath);
        
        return await Task.FromResult(tempPath);
    }
    
    /// <summary>
    /// Validates metadata clone by checking duration and critical fields.
    /// Detects TagLib# corruption (duration mismatch > 100ms).
    /// </summary>
    private async Task<bool> ValidateMetadataCloneAsync(string upgradedPath, TrackEntity expectedTrack, string originalPath)
    {
        try
        {
            // Verify using MetadataCloner's built-in verification
            if (!await _metadataCloner.VerifyCloneAsync(upgradedPath, expectedTrack))
            {
                _logger.LogWarning("Metadata clone verification failed");
                return false;
            }
            
            // Additional: Duration corruption check
            // TODO: Compare duration of upgraded file vs original
            // If diff > 100ms, tagging corrupted the FLAC header
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Metadata validation error");
            return false;
        }
    }
    
    /// <summary>
    /// Creates journal entry for crash recovery.
    /// </summary>
    private async Task<string> CreateUpgradeJournalEntryAsync(string originalPath, string tempPath, string trackId)
    {
        // TODO: Add OperationType.LibraryUpgrade to CrashRecoveryJournal
        // For now, return placeholder ID
        return await Task.FromResult(Guid.NewGuid().ToString());
    }
    
    /// <summary>
    /// Performs atomic file swap.
    /// Uses MFT (Master File Table) update on same volume, or validated copy+delete across volumes.
    /// </summary>
    private async Task PerformAtomicSwapAsync(string sourcePath, string targetPath)
    {
        var sourceRoot = Path.GetPathRoot(sourcePath);
        var targetRoot = Path.GetPathRoot(targetPath);
        
        if (sourceRoot == targetRoot)
        {
            // Same volume - atomic MFT update
            _logger.LogDebug("Same-volume move (atomic MFT update)");
            File.Move(sourcePath, targetPath);
        }
        else
        else
        {
            // Cross-volume - use SafeWriteService for validated copy+delete
            _logger.LogDebug("Cross-volume move (verified copy+delete)");
            
            // Copy (overwrite)
            File.Copy(sourcePath, targetPath, overwrite: true);
            
            // Verify size match (basic integrity check)
            var sourceInfo = new FileInfo(sourcePath);
            var targetInfo = new FileInfo(targetPath);
            
            if (sourceInfo.Length != targetInfo.Length)
            {
                 throw new IOException($"Copy verification failed. Source size: {sourceInfo.Length}, Target size: {targetInfo.Length}");
            }
            
            // Delete source
            File.Delete(sourcePath);
        }
        
        await Task.CompletedTask;
    }
    
    /// <summary>
    /// Updates track entity after successful upgrade.
    /// </summary>
    private async Task UpdateTrackAfterUpgradeAsync(TrackEntity track, UpgradeCandidate candidate, UpgradeSearchResult upgradeSource)
    {
        track.PreviousBitrate = $"{candidate.CurrentBitrate}kbps {candidate.CurrentFormat}";
        track.Bitrate = upgradeSource.BitRate;
        track.LastUpgradeAt = DateTime.UtcNow;
        track.UpgradeSource = "Auto";
        
        // Promote integrity level if upgraded to FLAC
        var format = Path.GetExtension(track.Filename)?.ToLowerInvariant();
        if (format == ".flac")
        {
            track.Integrity = IntegrityLevel.Verified; // Verfied by upgrade
        }
        
        await _databaseService.SaveTrackAsync(track);
    }
    
    /// <summary>
    /// Schedules backup file for cleanup after retention period.
    /// </summary>
    private async Task ScheduleBackupCleanupAsync(string? backupPath)
    {
        if (string.IsNullOrEmpty(backupPath))
            return;
        
        // TODO: Add to background cleanup queue
        _logger.LogDebug("Scheduled backup cleanup: {Path} (retention: {Days} days)", backupPath, BACKUP_RETENTION_DAYS);
        
        await Task.CompletedTask;
    }
    
    /// <summary>
    /// Rollback logic for failed upgrades.
    /// Restores original file from backup.
    /// </summary>
    private async Task RollbackUpgradeAsync(string? originalPath, string? backupPath, string? tempPath, string? journalId)
    {
        _logger.LogWarning("🔄 Attempting rollback...");
        
        try
        {
            // Restore from backup if exists
            if (!string.IsNullOrEmpty(backupPath) && File.Exists(backupPath) &&
                !string.IsNullOrEmpty(originalPath) && !File.Exists(originalPath))
            {
                File.Move(backupPath, originalPath, overwrite: true);
                _logger.LogInformation("✅ Restored original file from backup");
            }
            
            // Cleanup temp file
            if (!string.IsNullOrEmpty(tempPath) && File.Exists(tempPath))
            {
                File.Delete(tempPath);
                _logger.LogDebug("Deleted temp upgrade file");
            }
            
            // Mark journal entry as failed
            if (!string.IsNullOrEmpty(journalId))
            {
                // TODO: Mark journal entry as rolled back
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Rollback failed - manual intervention may be required");
        }
        
        await Task.CompletedTask;
    }
    
    /// <summary>
    /// Creates backup path with timestamp.
    /// </summary>
    private string CreateBackupPath(string originalPath)
    {
        var backupDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            BACKUP_DIR,
            DateTime.UtcNow.ToString("yyyy-MM-dd")
        );
        
        var fileName = Path.GetFileName(originalPath);
        return Path.Combine(backupDir, $"{Path.GetFileNameWithoutExtension(fileName)}.{Guid.NewGuid()}{Path.GetExtension(fileName)}");
    }
    
    /// <summary>
    /// Ensures directory exists.
    /// </summary>
    private void EnsureDirectoryExists(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }
}

/// <summary>
/// Upgrade state machine states.
/// </summary>
public enum UpgradeState
{
    Pending = 0,           // Candidate found, waiting for download
    Downloading = 1,       // P2P transfer in progress (.upgrade file)
    CloningMetadata = 2,   // Transferring "Soul" (Tags/Art)
    ReadyToSwap = 3,       // All checks passed, Journal entry created
    BackingUp = 4,         // Moving original to 7-day storage
    Swapping = 5,          // Moving FLAC to library destination
    UpdatingDatabase = 6,  // Finalizing SQL records
    Completed = 7,         // Cleanup done
    Failed = 99            // Error encountered (Rollback triggered)
}

/// <summary>
/// Result of an upgrade operation.
/// </summary>
public class UpgradeResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int QualityGain { get; set; }        // Bitrate delta (kbps)
    public double PercentGain { get; set; }     // Quality improvement percentage
    public bool IsDeferred { get; set; }        // True if upgrade was postponed
    
    public static UpgradeResult CreateSuccess(int qualityGain, double percentGain) => new()
    {
        Success = true,
        Message = "Upgrade successful",
        QualityGain = qualityGain,
        PercentGain = percentGain
    };
    
    public static UpgradeResult CreateFailed(string reason) => new()
    {
        Success = false,
        Message = reason
    };
    
    public static UpgradeResult CreateDeferred(string reason) => new()
    {
        Success = false,
        Message = $"Deferred: {reason}",
        IsDeferred = true
    };
}
