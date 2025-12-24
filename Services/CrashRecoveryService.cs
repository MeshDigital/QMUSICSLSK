using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using SLSKDONET.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace SLSKDONET.Services;

public class RecoveryStats
{
    public int Resumed { get; set; }
    public int Cleaned { get; set; }
    public int Failures { get; set; }
    public int DeadLetters { get; set; }
}

/// <summary>
/// Phase 2A: Recovers interrupted operations on application startup.
/// Uses priority-based recovery, dead-letter handling, and path sanitization.
/// </summary>
public class CrashRecoveryService
{
    private readonly ILogger<CrashRecoveryService> _logger;
    private readonly CrashRecoveryJournal _journal;
    private readonly DatabaseService _databaseService;
    // Note: DownloadManager will be added later to avoid circular dependency

    public CrashRecoveryService(
        ILogger<CrashRecoveryService> logger,
        CrashRecoveryJournal journal,
        DatabaseService databaseService)
    {
        _logger = logger;
        _journal = journal;
        _databaseService = databaseService;
    }

    /// <summary>
    /// Called on application startup to recover from crashes.
    /// Runs asynchronously to avoid blocking UI.
    /// </summary>
    public async Task RecoverAsync()
    {
        _logger.LogInformation("🔧 Starting Ironclad Recovery...");

        // ASYNC TRAP FIX: Run recovery on background thread
        await Task.Run(async () =>
        {
            try
            {
                // STEP 1: Clear truly stale checkpoints (>24 hours)
                await _journal.ClearStaleCheckpointsAsync();

                // STEP 2: Get pending checkpoints
                var pendingCheckpoints = await _journal.GetPendingCheckpointsAsync();
                
                if (!pendingCheckpoints.Any())
                {
                    _logger.LogInformation("✅ No pending operations to recover");
                    return;
                }

                _logger.LogInformation("🔄 Recovering {Count} operations...", pendingCheckpoints.Count);

                var stats = new RecoveryStats();

                // STEP 3: Recover each operation (already sorted by priority)
                foreach (var checkpoint in pendingCheckpoints)
                {
                    try
                    {
                        // DEAD-LETTER CHECK
                        if (checkpoint.FailureCount >= 3)
                        {
                            _logger.LogWarning(
                                "⚠️ Checkpoint {Id} failed {Count} times - moving to dead-letter",
                                checkpoint.Id, checkpoint.FailureCount);
                            
                            await LogDeadLetterAsync(checkpoint);
                            await _journal.CompleteCheckpointAsync(checkpoint.Id);
                            stats.DeadLetters++;
                            continue;
                        }

                        switch (checkpoint.OperationType)
                        {
                            case OperationType.Download:
                                await RecoverDownloadAsync(checkpoint, stats);
                                break;

                            case OperationType.TagWrite:
                                await RecoverTagWriteAsync(checkpoint, stats);
                                break;

                            case OperationType.MetadataHydration:
                                await RecoverHydrationAsync(checkpoint, stats);
                                break;

                            default:
                                _logger.LogWarning("Unknown operation type: {Type}", checkpoint.OperationType);
                                await _journal.CompleteCheckpointAsync(checkpoint.Id);
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Recovery failed for checkpoint {Id}", checkpoint.Id);
                        
                        // Increment failure count
                        checkpoint.FailureCount++;
                        await _journal.LogCheckpointAsync(checkpoint);
                        stats.Failures++;
                    }
                }

                _logger.LogInformation(
                    "✅ Recovery complete: {Resumed} resumed, {Cleaned} cleaned, {Failed} failed, {DeadLetters} dead-letters",
                    stats.Resumed, stats.Cleaned, stats.Failures, stats.DeadLetters);

                // TODO: Phase 2A Step 6 - Publish RecoveryCompletedEvent for UX
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal error during crash recovery");
            }
        });
    }

    private async Task RecoverDownloadAsync(RecoveryCheckpoint checkpoint, RecoveryStats stats)
    {
        var state = JsonSerializer.Deserialize<DownloadCheckpointState>(checkpoint.StateJson);
        if (state == null)
        {
            _logger.LogWarning("Invalid checkpoint state for {Id}", checkpoint.Id);
            await _journal.CompleteCheckpointAsync(checkpoint.Id);
            return;
        }

        _logger.LogInformation("Recovering download: {Artist} - {Title}", state.Artist, state.Title);

        // SECURITY: Path sanitization
        try
        {
            var partPath = Path.GetFullPath(state.PartFilePath);
            var finalPath = Path.GetFullPath(state.FinalPath);
            
            // Verify paths are safe (not absolute paths outside download directory)
            // For now, just check they don't contain ".." or system directories
            if (partPath.Contains("..") || finalPath.Contains(".."))
            {
                _logger.LogWarning("⚠️ Suspicious path detected in checkpoint: {Path}", partPath);
                await _journal.CompleteCheckpointAsync(checkpoint.Id);
                return;
            }

            if (File.Exists(state.PartFilePath))
            {
                var partSize = new FileInfo(state.PartFilePath).Length;
                
                // Check if download appears complete (95% threshold to account for metadata)
                if (partSize >= state.ExpectedSize * 0.95)
                {
                    // Nearly complete - verify and finalize
                    _logger.LogInformation("Download appears complete ({Size}/{Expected}), verifying...", 
                        partSize, state.ExpectedSize);
                    
                    var isValid = await SLSKDONET.Services.IO.FileVerificationHelper.VerifyAudioFormatAsync(state.PartFilePath);
                    
                    if (isValid)
                    {
                        // Atomic rename
                        if (File.Exists(state.FinalPath))
                        {
                            File.Delete(state.FinalPath);
                        }
                        File.Move(state.PartFilePath, state.FinalPath);
                        
                        _logger.LogInformation("✅ Recovered and finalized download: {Path}", state.FinalPath);
                        
                        await _journal.CompleteCheckpointAsync(checkpoint.Id);
                        stats.Resumed++;
                        return;
                    }
                    else
                    {
                        _logger.LogWarning("Downloaded file failed verification, deleting: {Path}", state.PartFilePath);
                        File.Delete(state.PartFilePath);
                        await _journal.CompleteCheckpointAsync(checkpoint.Id);
                        stats.Cleaned++;
                        return;
                    }
                }
                
                // Partial download - log for manual re-queue
                _logger.LogInformation("📥 Partial download found: {Path} ({Percent}%)",
                    state.PartFilePath, (partSize * 100.0 / state.ExpectedSize));
                
                // TODO: Re-queue download (requires DownloadManager injection)
                // For now, keep checkpoint for next startup
                _logger.LogInformation("Keeping checkpoint for future resume attempt");
            }
            else
            {
                // EXTERNAL INTERFERENCE: No .part file
                _logger.LogInformation("No .part file found, checking database state...");
                
                // Check if track is stuck in "Downloading" state
                // TODO: Needs track lookup by GlobalId
                
                _logger.LogInformation("Cleaning up orphaned checkpoint (no .part file)");
                await _journal.CompleteCheckpointAsync(checkpoint.Id);
                stats.Cleaned++;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to recover download for {Title}", state.Title);
            throw;
        }
    }

    private async Task RecoverTagWriteAsync(RecoveryCheckpoint checkpoint, RecoveryStats stats)
    {
        var state = JsonSerializer.Deserialize<TagWriteCheckpointState>(checkpoint.StateJson);
        if (state== null)
        {
            await _journal.CompleteCheckpointAsync(checkpoint.Id);
            return;
        }

        _logger.LogInformation("Recovering tag write: {Path}", state.FilePath);

        // Clean up orphaned temp file if it exists
        if (!string.IsNullOrEmpty(state.TempPath) && File.Exists(state.TempPath))
        {
            try
            {
                File.Delete(state.TempPath);
                _logger.LogInformation("🗑️ Cleaned up orphaned temp file: {Path}", state.TempPath);
                stats.Cleaned++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete temp file: {Path}", state.TempPath);
            }
        }

        await _journal.CompleteCheckpointAsync(checkpoint.Id);
    }

    private async Task RecoverHydrationAsync(RecoveryCheckpoint checkpoint, RecoveryStats stats)
    {
        var state = JsonSerializer.Deserialize<HydrationCheckpointState>(checkpoint.StateJson);
        if (state == null)
        {
            await _journal.CompleteCheckpointAsync(checkpoint.Id);
            return;
        }

        _logger.LogInformation("Recovering metadata hydration: Track {Id}, Step {Step}", 
            state.TrackGlobalId, state.Step);

        // TODO: Re-queue for enrichment
        // For now, just log and clear
        _logger.LogInformation("Metadata hydration recovery not yet implemented");
        
        await _journal.CompleteCheckpointAsync(checkpoint.Id);
        stats.Cleaned++;
    }

    private async Task LogDeadLetterAsync(RecoveryCheckpoint checkpoint)
    {
        try
        {
            var deadLetterPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SLSKDONET", "dead_letters.log");
            
            var logEntry = $"[{DateTime.UtcNow:O}] DEAD_LETTER | Type: {checkpoint.OperationType} | " +
                          $"Path: {checkpoint.TargetPath} | Failures: {checkpoint.FailureCount} | " +
                          $"State: {checkpoint.StateJson}\n";
            
            await File.AppendAllTextAsync(deadLetterPath, logEntry);
            
            _logger.LogWarning("Dead-letter logged to: {Path}", deadLetterPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log dead-letter");
        }
    }
}
