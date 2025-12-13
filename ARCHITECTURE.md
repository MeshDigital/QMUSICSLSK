# SLSKDONET Architecture & Data Flow

## System Overview

```
┌─────────────────────────────────────────────────────────────┐
│                         UI Layer                            │
│  ┌─────────────────────────────┐   ┌──────────────────────┐ │
│  │ MainWindow (navigation shell)│  │ WPF Pages (Search,   │ │
│  │ ├─ NavigationService         │  │ Library, Downloads,  │ │
│  │ ├─ PlayerViewModel           │  │ Settings, History)   │ │
│  │ └─ Drag-and-Drop Adorners    │  │                      │ │
│  └──────────────┬───────────────┘   └───────────┬──────────┘ │
│                 │                               │            │
│                 ▼                               ▼            │
│        ┌─────────────────────────────────────────────────┐   │
│        │              MainViewModel (App Brain)          │   │
│        │  - Commands for navigation & orchestration      │   │
│        │  - Surface collections (SearchResults, Library) │   │
│        │  - Connection status & global state             │   │
│        └───────────────┬─────────────────────────────────┘   │
└────────────────────────┼──────────────────────────────────────┘
                         │
        ┌────────────────▼────────────────┐
        │   Application Services          │
        │  DownloadManager                │
        │  LibraryService                 │
        │  AudioPlayerService (LibVLC)    │
        │  ImportOrchestrator             │
        │  MetadataService                │
        └────────────┬────────────────────┘
                     │
        ┌────────────▼────────────────┐
        │ Infrastructure Layer        │
        │  SoulseekAdapter            │
        │  DatabaseService (EF Core)  │
        │  ConfigManager (INI)        │
        │  Import Providers           │
        │   ├─ SpotifyImportProvider  │
        │   ├─ CsvImportProvider      │
        │   └─ ManualImportProvider   │
        └─────────────────────────────┘
```

The application uses `Microsoft.Extensions.DependencyInjection` for service wiring in `App.xaml.cs`. All services are singletons unless otherwise specified.

## Navigation Shell

- `MainWindow` hosts a left-hand navigation rail with `Frame` navigation managed by `NavigationService`
- Pages share the same `MainViewModel` instance for cross-page state
- Global status bar shows connection state, download progress, and version number
- Player sidebar (collapsible) for audio playback control

## Import Pipeline

```
User action (Spotify URL / CSV file / manual query)
                │
                ▼
┌────────────────────────────┐
│ Import Providers           │
│  - SpotifyImportProvider   │
│  - CsvImportProvider       │
│  - ManualImportProvider    │
└──────────────┬─────────────┘
               │ ImportResult
               ▼
┌────────────────────────────┐
│ ImportOrchestrator         │
│  - Validates tracks        │
│  - Creates PlaylistJob     │
│  - Persists to database    │
└──────────────┬─────────────┘
               │
               ▼
DownloadManager.QueueProject → global processing loop
```

## Download Orchestration

- `DownloadManager` maintains `ObservableCollection<PlaylistTrackViewModel>` (`AllGlobalTracks`)
- Tracks flow through states: Pending → Searching → Downloading → Completed/Failed
- `SemaphoreSlim` enforces `MaxConcurrentDownloads` from configuration
- Metadata enrichment (album art, tagging) runs asynchronously via `IMetadataService`
- Events: `TrackUpdated`, `ProjectAdded`, `ProjectUpdated` notify ViewModels

## Audio Playback System

```
User action (double-click track / drag to player)
                │
                ▼
┌────────────────────────────┐
│ PlayerViewModel            │
│  - PlayTrack(path)         │
│  - Pause/Resume/Stop       │
│  - Volume control          │
└──────────────┬─────────────┘
               │
               ▼
┌────────────────────────────┐
│ IAudioPlayerService        │
│  (AudioPlayerService)      │
│  - LibVLC wrapper          │
│  - IsInitialized check     │
│  - Media player instance   │
└────────────────────────────┘
```

**LibVLC Integration**:
- Native libraries loaded from `libvlc/` folder in output directory
- Initialization checked on startup; UI shows error if libraries missing
- Supports MP3, FLAC, WAV, and other common formats

## Drag-and-Drop System

```
User drags track from DataGrid
                │
                ▼
┌────────────────────────────┐
│ LibraryPage.xaml.cs        │
│  - MouseDown handler       │
│  - DragAdorner creation    │
│  - DoDragDrop call         │
└──────────────┬─────────────┘
               │
               ▼
┌────────────────────────────┐
│ Drop Targets               │
│  - Playlist ListBox        │
│  - Player sidebar          │
└──────────────┬─────────────┘
               │
               ▼
┌────────────────────────────┐
│ LibraryViewModel           │
│  - AddToPlaylist()         │
│  - Resolves file paths     │
│  - Persists to database    │
│  - Reloads UI              │
└────────────────────────────┘
```

**Visual Feedback**:
- `DragAdorner` shows track info during drag
- Console logging (`[DRAG]` prefix) for diagnostics

## Persistence Layer

- `DatabaseService` wraps `AppDbContext` (EF Core + SQLite):
  - `LibraryEntryEntity`: Global library index by `UniqueHash`
  - `PlaylistJobEntity`: Playlist/job headers with soft delete
  - `PlaylistTrackEntity`: Per-track status and file metadata
  - `PlaylistActivityLogEntity`: Activity history for playlists
- Database: `%AppData%\SLSKDONET\library.db`
- Schema created automatically on startup with migration support
- `LibraryService` provides domain-friendly conversions

## Configuration & Secrets

- `ConfigManager` manages `%AppData%\SLSKDONET\config.ini`
- `AppConfig` injected into services
- `ProtectedDataService` encrypts passwords using Windows DPAPI
- Runtime settings changes persist immediately via `SaveSettingsCommand`

## Diagnostics System

### Console Output (Debug Builds)
- Debug builds (`OutputType=Exe`) show console window
- Release builds (`OutputType=WinExe`) hide console
- All `Console.WriteLine()` and `Debug.WriteLine()` visible in console
- Prefixes: `[DRAG]`, `[PLAYBACK]`, `info:`, `warn:`, `fail:`

### UI Diagnostics
- Version number displayed in status bar
- Player initialization status shown in player sidebar
- Connection status in status bar
- Download progress with real-time counters

## Eventing & Status Propagation

- `SoulseekAdapter` exposes `EventBus` (Reactive `Subject<>`) for network events
- `MainViewModel` maps adapter state to UI properties (`IsConnected`, `StatusText`)
- `DownloadManager` fires `TrackUpdated` for progress updates
- `LibraryService` fires `ProjectDeleted`, `ProjectUpdated` for library changes

## Extensibility Points

- **Import Providers**: Implement `IImportProvider` and register with `ImportOrchestrator`
- **Metadata Services**: Add alternative `IMetadataService` implementations
- **Audio Backends**: Replace `IAudioPlayerService` implementation
- **UI Pages**: Register additional pages via `NavigationService.RegisterPage`

This architecture follows MVVM best practices, centralizes orchestration in ViewModels, and uses SQLite for persistence to support large-scale batch operations across sessions.
