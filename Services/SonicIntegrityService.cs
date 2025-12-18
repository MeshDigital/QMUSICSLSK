using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SLSKDONET.Models;

namespace SLSKDONET.Services;

/// <summary>
/// Result of a sonic integrity analysis.
/// </summary>
public class SonicAnalysisResult
{
    public double QualityConfidence { get; set; } // 0.0 - 1.0
    public int FrequencyCutoff { get; set; } // Hz
    public string SpectralHash { get; set; } = string.Empty;
    public bool IsTrustworthy { get; set; }
    public string? Details { get; set; }
}

/// <summary>
/// Service for validating audio fidelity using spectral analysis (headless FFmpeg).
/// </summary>
public class SonicIntegrityService
{
    private readonly ILogger<SonicIntegrityService> _logger;
    private readonly string _ffmpegPath = "ffmpeg"; // Assume in path for now, can be configured

    public SonicIntegrityService(ILogger<SonicIntegrityService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Performs spectral analysis on an audio file to detect upscaling or low-quality VBR.
    /// </summary>
    public async Task<SonicAnalysisResult> AnalyzeTrackAsync(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Audio file not found for analysis", filePath);

        try
        {
            _logger.LogInformation("Starting sonic integrity analysis for: {File}", Path.GetFileName(filePath));

            // Stage 1: Check energy above 16kHz (Cutoff for 128kbps)
            double energy16k = await GetEnergyAboveFrequencyAsync(filePath, 16000);
            
            // Stage 2: Check energy above 19kHz (Cutoff for 256k/320k)
            double energy19k = await GetEnergyAboveFrequencyAsync(filePath, 19000);

            // Stage 3: Check energy above 21kHz (True Lossless/High-Res)
            double energy21k = await GetEnergyAboveFrequencyAsync(filePath, 21000);

            _logger.LogDebug("Energy Profile for {File}: 16k={E16}dB, 19k={E19}dB, 21k={E21}dB", 
                Path.GetFileName(filePath), energy16k, energy19k, energy21k);

            int cutoff = 0;
            double confidence = 1.0;
            bool trustworthy = true;
            string details = "";

            if (energy16k < -55)
            {
                cutoff = 16000;
                confidence = 0.3; // Very likely an upscale if reported as FLAC/320k
                trustworthy = energy16k > -70; // If it's -90, it's a hard cutoff (fake)
                details = "FAKED: Low-quality upscale (128kbps profile)";
            }
            else if (energy19k < -55)
            {
                cutoff = 19000;
                confidence = 0.7;
                details = "MID-QUALITY: 192kbps profile detected";
            }
            else if (energy21k < -50)
            {
                cutoff = 21000;
                confidence = 0.9;
                details = "HIGH-QUALITY: 320kbps profile detected";
            }
            else
            {
                cutoff = 22050; // Standard Full Spectrum
                confidence = 1.0;
                details = "AUDIOPHILE: Full frequency spectrum confirmed";
            }

            // Simple spectral hash based on energy ratios
            string spectralHash = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{energy16k:F1}|{energy19k:F1}")).Substring(0, 8);

            return new SonicAnalysisResult
            {
                QualityConfidence = confidence,
                FrequencyCutoff = cutoff,
                SpectralHash = spectralHash,
                IsTrustworthy = trustworthy,
                Details = details
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sonic analysis failed for {File}", filePath);
            return new SonicAnalysisResult { IsTrustworthy = false, Details = "Analysis error: " + ex.Message };
        }
    }

    private async Task<double> GetEnergyAboveFrequencyAsync(string filePath, int freq)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _ffmpegPath,
            Arguments = $"-i \"{filePath}\" -af \"highpass=f={freq},volumedetect\" -f null -",
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        var output = new StringBuilder();
        process.ErrorDataReceived += (s, e) => { if (e.Data != null) output.AppendLine(e.Data); };

        process.Start();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();

        string result = output.ToString();
        // Parse "max_volume: -24.5 dB"
        var match = System.Text.RegularExpressions.Regex.Match(result, @"max_volume:\s+(-?\d+\.?\d*)\s+dB");
        if (match.Success && double.TryParse(match.Groups[1].Value, out double vol))
        {
            return vol;
        }

        return -91.0; // Assume silence if parsing fails
    }
}
