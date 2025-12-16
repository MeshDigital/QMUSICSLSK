using SLSKDONET.ViewModels;

namespace SLSKDONET.Models;

/// <summary>
/// Typed event records for the EventBus system.
/// These replace anonymous tuples and custom event handlers.
/// </summary>

// Download Manager Events
public record TrackUpdatedEvent(PlaylistTrackViewModel Track);
public record ProjectAddedEvent(Guid ProjectId);
public record ProjectUpdatedEvent(Guid ProjectId);
public record ProjectDeletedEvent(Guid ProjectId);

// Soulseek Adapter Events
public record SoulseekStateChangedEvent(string State, bool IsConnected);
public record SoulseekConnectionStatusEvent(string Status, string Username);
public record TransferProgressEvent(string Filename, string Username, long BytesTransferred, long TotalBytes);
public record TransferFinishedEvent(string Filename, string Username);
public record TransferCancelledEvent(string Filename, string Username);
public record TransferFailedEvent(string Filename, string Username, string Error);

// Library Service Events
public record LibraryEntryAddedEvent(LibraryEntry Entry);
public record LibraryEntryUpdatedEvent(LibraryEntry Entry);
public record LibraryEntryDeletedEvent(string UniqueHash);

// Player Events
public record TrackPlaybackStartedEvent(string FilePath, string Artist, string Title);
public record TrackPlaybackPausedEvent();
public record TrackPlaybackResumedEvent();
public record TrackPlaybackStoppedEvent();
public record PlaybackProgressEvent(TimeSpan Position, TimeSpan Duration);
