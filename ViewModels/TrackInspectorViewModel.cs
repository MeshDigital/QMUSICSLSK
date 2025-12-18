using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using SLSKDONET.Models;

namespace SLSKDONET.ViewModels
{
    public class TrackInspectorViewModel : INotifyPropertyChanged
    {
        private PlaylistTrack? _track;
        public PlaylistTrack? Track
        {
            get => _track;
            set
            {
                if (SetProperty(ref _track, value))
                {
                    OnPropertyChanged(nameof(HasTrack));
                    OnPropertyChanged(nameof(CamelotKey));
                    OnPropertyChanged(nameof(BitrateLabel));
                    OnPropertyChanged(nameof(AudioGuardColor));
                    OnPropertyChanged(nameof(AudioGuardIcon));
                    OnPropertyChanged(nameof(FrequencyCutoffLabel));
                    OnPropertyChanged(nameof(ConfidenceLabel));
                    OnPropertyChanged(nameof(IsTrustworthy));
                    OnPropertyChanged(nameof(Details));
                    OnPropertyChanged(nameof(TrustColor));
                }
            }
        }

        public bool HasTrack => Track != null;

        public string CamelotKey => MapToCamelot(Track?.Key);

        public string BitrateLabel => Track?.Bitrate > 0 ? $"{Track.Bitrate} kbps" : "Unknown Bitrate";

        public string AudioGuardColor => GetAudioGuardColor();
        public string AudioGuardIcon => GetAudioGuardIcon();

        public string FrequencyCutoffLabel => Track?.FrequencyCutoff > 0 ? $"{Track.FrequencyCutoff / 1000.0:F1} kHz" : "Analysing...";
        public string ConfidenceLabel => Track?.QualityConfidence >= 0 ? $"{Track.QualityConfidence:P0}" : "??%";
        public bool IsTrustworthy => Track?.IsTrustworthy ?? true;
        public string Details => Track?.QualityDetails ?? "Analysis pending or no data available.";
        public string TrustColor => IsTrustworthy ? "#1DB954" : "#D32F2F";

        public event PropertyChangedEventHandler? PropertyChanged;

        private string MapToCamelot(string? key)
        {
            if (string.IsNullOrEmpty(key)) return "??";

            // Basic mapping for common key formats (e.g., "C Major", "Am", "8A")
            return key.ToUpper() switch
            {
                "C" or "C MAJOR" or "8B" => "8B",
                "AM" or "A MINOR" or "8A" => "8A",
                "G" or "G MAJOR" or "9B" => "9B",
                "EM" or "E MINOR" or "9A" => "9A",
                "D" or "D MAJOR" or "10B" => "10B",
                "BM" or "B MINOR" or "10A" => "10A",
                "A" or "A MAJOR" or "11B" => "11B",
                "F#M" or "F# MINOR" or "11A" => "11A",
                "E" or "E MAJOR" or "12B" => "12B",
                "C#M" or "C# MINOR" or "12A" => "12A",
                "B" or "B MAJOR" or "1B" => "1B",
                "G#M" or "G# MINOR" or "1A" => "1A",
                "F#" or "F# MAJOR" or "Gb" or "2B" => "2B",
                "D#M" or "D# MINOR" or "EBM" or "2A" => "2A",
                "C#" or "C# MAJOR" or "Db" or "3B" => "3B",
                "A#M" or "A# MINOR" or "BBM" or "3A" => "3A",
                "G#" or "G# MAJOR" or "Ab" or "4B" => "4B",
                "FM" or "F MINOR" or "4A" => "4A",
                "D#" or "D# MAJOR" or "Eb" or "5B" => "5B",
                "CM" or "C MINOR" or "5A" => "5A",
                "A#" or "A# MAJOR" or "Bb" or "6B" => "6B",
                "GM" or "G MINOR" or "6A" => "6A",
                "F" or "F MAJOR" or "7B" => "7B",
                "DM" or "D MINOR" or "7A" => "7A",
                _ => key
            };
        }

        private string GetAudioGuardColor()
        {
            if (Track == null) return "#333333";
            if (Track.Bitrate >= 1000 || (Track.Format?.Equals("FLAC", StringComparison.OrdinalIgnoreCase) ?? false)) return "#00A3FF"; // Lossless
            if (Track.Bitrate >= 320) return "#1DB954"; // High Quality
            if (Track.Bitrate >= 192) return "#FFCC00"; // Mid Quality
            return "#D32F2F"; // Low Quality
        }

        private string GetAudioGuardIcon()
        {
            if (Track == null) return "❓";
            if (Track.Bitrate >= 1000 || (Track.Format?.Equals("FLAC", StringComparison.OrdinalIgnoreCase) ?? false)) return "💎";
            if (Track.Bitrate >= 320) return "✅";
            if (Track.Bitrate >= 192) return "⚠️";
            return "❌";
        }

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (System.Collections.Generic.EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
