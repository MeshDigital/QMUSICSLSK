using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using SLSKDONET.Data;
using SLSKDONET.Models;
using SLSKDONET.Views;

namespace SLSKDONET.ViewModels;

public enum UpgradeStatus
{
    Pending,
    Searching,
    Ready,
    Upgrading,
    Completed,
    Failed
}

public class UpgradeCandidateViewModel : INotifyPropertyChanged
{
    private readonly TrackEntity _entity;
    private UpgradeStatus _status = UpgradeStatus.Pending;
    private Track? _proposedReplacement;
    private string? _statusMessage;

    public UpgradeCandidateViewModel(TrackEntity entity)
    {
        _entity = entity;
    }

    public string GlobalId => _entity.GlobalId;
    public string Artist => _entity.Artist;
    public string Title => _entity.Title;
    public int? CurrentBitrate => _entity.Bitrate;
    public bool IsFaked => _entity.IsTrustworthy == false;

    public UpgradeStatus Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusDisplay)); }
    }

    public string StatusDisplay => Status switch
    {
        UpgradeStatus.Pending => "Waiting",
        UpgradeStatus.Searching => "Searching...",
        UpgradeStatus.Ready => "Ready for Upgrade",
        UpgradeStatus.Upgrading => "Upgrading...",
        UpgradeStatus.Completed => "Success",
        UpgradeStatus.Failed => "No match found",
        _ => "Unknown"
    };

    public string? StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(); }
    }

    public Track? ProposedReplacement
    {
        get => _proposedReplacement;
        set { _proposedReplacement = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) 
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
