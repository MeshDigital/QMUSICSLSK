# SLSKDONET Architecture & Data Flow

## System Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                        WPF GUI Layer                            │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │  MainWindow (XAML)                                       │  │
│  │  - Login Tab, Search Tab, Downloads Tab                 │  │
│  └────────────────────────┬─────────────────────────────────┘  │
│                           │                                      │
│  ┌────────────────────────▼─────────────────────────────────┐  │
│  │  MainViewModel (MVVM)                                    │  │
│  │  - Binding layer between UI & Services                  │  │
│  │  - SearchResults, Downloads (ObservableCollection)      │  │
│  └────────────────────────┬─────────────────────────────────┘  │
└────────────────────────────┼──────────────────────────────────────┘
                             │
     ┌───────────────────────┼───────────────────────┐
     │                       │                       │
     ▼                       ▼                       ▼
┌──────────────────┐  ┌──────────────────┐  ┌──────────────────┐
│ SoulseekAdapter  │  │ DownloadManager  │  │ FileNameFormatter│
│ - Connect()      │  │ - Enqueue()      │  │ - Format()       │
│ - Search()       │  │ - Start()        │  │ (Template-based) │
│ - Download()     │  │ - Cancel()       │  │                  │
│ - EventBus       │  │ - Track jobs     │  └──────────────────┘
└────────┬─────────┘  └────────┬─────────┘
         │                     │
         │  EventBus:          │  JobUpdated/
         │  - connection       │  Completed
         │  - search_results   │  events
         │  - transfer_*       │
         │                     │
         └──────────────────────┘
                  ▲
                  │
         ┌────────┴──────────┐
         │                   │
    ┌────▼─────┐      ┌──────▼───────┐
    │  Soulseek│      │ Search Query │
    │  .NET    │      │ Normalizer   │
    │  Library │      │ - Remove "ft"│
    │          │      │ - Regex      │
    └──────────┘      └──────────────┘
```

## Input Processing Flow

```
User Input (Search Query)
        │
        ▼
┌─────────────────────┐
│ InputType Detection │
├─────────────────────┤
│ - CSV file?         │
│ - Spotify URL?      │
│ - YouTube URL?      │
│ - Direct string?    │
└────────┬────────────┘
         │
    ┌────┴────────────────────┬──────────────┬─────────────┐
    │                         │              │             │
    ▼                         ▼              ▼             ▼
┌─────────┐           ┌──────────┐    ┌────────────┐  ┌────────┐
│ CSV     │           │ String   │    │ Spotify    │  │ YouTube│
│ Source  │           │ Source   │    │ Source     │  │ Source │
└────┬────┘           └────┬─────┘    └────┬───────┘  └───┬────┘
     │                     │               │             │
     ├─ Auto-detect cols   └─ Parse        ├─ OAuth      └─ yt-dlp
     └─ Build queries         format       └─ Playlist

         │
         ▼
    ┌──────────────┐
    │ SearchQuery  │
    │ Objects      │
    │ - title      │
    │ - artist     │
    │ - mode       │
    └──────┬───────┘
           │
           ▼
    ┌─────────────────────┐
    │ SearchQueryNormalizer│
    ├─────────────────────┤
    │ - RemoveFeatArtists()│
    │ - RemoveYoutubeMarkers
    │ - ApplyRegex()      │
    └──────┬──────────────┘
           │
           ▼
    ┌──────────────┐
    │ Search Ready │
    └──────────────┘
```

## Search & Filter Flow

```
Search Query
    │
    ▼
SoulseekAdapter.SearchAsync()
    │
    ├─ Query: "Daft Punk - Get Lucky"
    ├─ Timeout: 6000ms
    ├─ Max Results: 100
    │
    ▼
Soulseek Network
    │
    ├─ Returns: List<SearchResponse>
    │  - Username, Files[], etc.
    │
    ▼
┌──────────────────────────┐
│ Parse Results to Track[] │
├──────────────────────────┤
│ - Extract filename       │
│ - Parse bitrate          │
│ - Get size, sample rate  │
└──────┬───────────────────┘
       │
       ▼
┌──────────────────────────────────────┐
│ FileConditionEvaluator.FilterAndRank()
├──────────────────────────────────────┤
│ 1. Apply Required Conditions        │
│    ├─ Format in [mp3, flac]?        │
│    ├─ Bitrate >= 128?               │
│    └─ Pass: true/false              │
│                                      │
│ 2. Score Preferred Conditions       │
│    ├─ Format = mp3? (+1 point)      │
│    ├─ Bitrate in 200-2500? (+1)     │
│    ├─ Length within 3s? (+1)        │
│    └─ Score: 0.0 to 1.0             │
│                                      │
│ 3. Sort Results                     │
│    ├─ By preference score DESC      │
│    ├─ By bitrate DESC               │
│    └─ Final ranking                 │
└──────┬───────────────────────────────┘
       │
       ▼
┌────────────────────┐
│ UI: SearchResults  │
│ (Top results first)│
└────────────────────┘
```

## Download Flow

```
User Clicks "Add to Downloads"
    │
    ▼
┌──────────────────────────┐
│ DownloadManager          │
│ .EnqueueDownload()       │
├──────────────────────────┤
│ Create DownloadJob:      │
│ - Track                  │
│ - State: Pending         │
│ - OutputPath             │
└──────┬───────────────────┘
       │
       ▼
┌──────────────────────┐
│ Jobs in Queue        │
│ (ObservableCollection)
└──────┬───────────────┘
       │
       ▼ (User clicks "Start Downloads")
┌──────────────────────────────────┐
│ DownloadManager.StartAsync()     │
├──────────────────────────────────┤
│ Semaphore: Max 2 concurrent      │
│                                  │
│ For Each Job:                    │
│ ├─ Acquire semaphore slot        │
│ ├─ Job.State = Downloading       │
│ ├─ Call SoulseekAdapter          │
│ │  .DownloadAsync()              │
│ │                                │
│ ▼                                │
└────────┬─────────────────────────┘
         │
    ┌────┴─────────────────────┐
    │                          │
    ▼                          ▼
┌──────────────────┐    ┌──────────────────┐
│ Download Success │    │ Download Failed  │
├──────────────────┤    ├──────────────────┤
│ - Save file      │    │ - Error message  │
│ - State: Done    │    │ - State: Failed  │
│ - Progress: 100% │    │ - Retry option   │
│ - Emit event     │    │ - Emit event     │
└──────┬───────────┘    └──────┬───────────┘
       │                       │
       └───────────┬───────────┘
                   │
                   ▼
          ┌────────────────────┐
          │ UI Updates         │
          │ - Progress bar     │
          │ - Status message   │
          │ - Color change     │
          └────────────────────┘
```

## Configuration & State Flow

```
Application Start
    │
    ▼
┌────────────────────────────────────┐
│ ConfigManager.Load()               │
├────────────────────────────────────┤
│ Check paths in order:              │
│ 1. %AppData%\SLSKDONET\config.ini │
│ 2. Local directory                 │
│ 3. Create default if missing       │
└──────┬─────────────────────────────┘
       │
       ▼
┌────────────────────────────────────┐
│ Parse INI File                     │
├────────────────────────────────────┤
│ [Soulseek]                         │
│ - Username                         │
│ - Password                         │
│ - ListenPort                       │
│ - Timeouts, etc.                   │
│                                    │
│ [Download]                         │
│ - Directory                        │
│ - MaxConcurrent                    │
│ - NameFormat                       │
│ - Conditions (preferred)           │
└──────┬─────────────────────────────┘
       │
       ▼
┌────────────────────────────────────┐
│ AppConfig Object                   │
│ (Injected to services)             │
├────────────────────────────────────┤
│ SoulseekAdapter uses:              │
│ - Username, Password               │
│ - Timeouts                         │
│                                    │
│ DownloadManager uses:              │
│ - DownloadDirectory                │
│ - MaxConcurrentDownloads           │
│ - NameFormat                       │
│                                    │
│ UI uses:                           │
│ - For display defaults             │
└────────────────────────────────────┘
```

## Service Dependency Graph

```
App.xaml.cs (Bootstrapper)
    │
    ├─ Configures Services
    │
    ▼
IServiceProvider (DI Container)
    │
    ├─ Creates Singletons:
    │  ├─ AppConfig (from ConfigManager)
    │  ├─ ConfigManager
    │  ├─ SoulseekAdapter (uses AppConfig)
    │  ├─ DownloadManager (uses AppConfig, SoulseekAdapter)
    │  └─ ILogger
    │
    ├─ Creates Transients:
    │  ├─ MainViewModel (uses logger, config, adapter, manager)
    │  └─ MainWindow (uses MainViewModel)
    │
    ▼
MainWindow (Resolved on app start)
    │
    ├─ DataContext = MainViewModel
    │
    └─ MainViewModel connects everything:
       ├─ Calls adapter.ConnectAsync()
       ├─ Calls adapter.SearchAsync()
       ├─ Calls downloadManager.EnqueueDownload()
       ├─ Calls downloadManager.StartAsync()
       └─ Updates UI via INotifyPropertyChanged
```

## Event Flow Architecture

```
SoulseekAdapter.EventBus (Subject<(string, object)>)
    │
    ├─ connection_status → UI updates login
    ├─ search_results → UI updates grid
    ├─ transfer_added → UI adds row
    ├─ transfer_progress → UI updates progress bar
    ├─ transfer_finished → UI shows complete
    └─ transfer_failed → UI shows error

Also:

DownloadManager Events:
    ├─ JobUpdated → UI progress
    └─ JobCompleted → UI status

MainViewModel.PropertyChanged:
    ├─ IsConnected
    ├─ StatusText
    ├─ SearchResults (ObservableCollection)
    └─ Downloads (ObservableCollection)
```

## File Format Examples

### SearchQuery Parsing
```
Input: "Daft Punk - Get Lucky"
    ↓
Parse as shorthand (contains " - ")
    ↓
Output:
  Artist: "Daft Punk"
  Title: "Get Lucky"
  Mode: Normal
  
Input: "artist=Daft Punk,title=Get Lucky,length=244"
    ↓
Parse as properties (contains "=")
    ↓
Output:
  Artist: "Daft Punk"
  Title: "Get Lucky"
  Length: 244
  Mode: Normal
```

### File Naming Templates
```
Template: "{artist}/{album}/{track}. {title}"
Track data:
  - artist: "Metallica"
  - album: "Master of Puppets"
  - track: 1
  - title: "Battery"

Result: "Metallica/Master of Puppets/1. Battery"

Template: "{artist( - ){title|filename}"
Without artist:
  Result: "Unknown.mp3"
With artist:
  Result: "Artist - Song Title"
```

### CSV Processing
```
CSV File:
  Artist,Title,Album,Length
  Daft Punk,Get Lucky,Random Access Memories,244
  The Weeknd,Blinding Lights,After Hours,200

Processing:
  1. Detect column names (auto)
  2. For each row → SearchQuery
  3. Convert Length to int (seconds)
  4. Create DownloadMode based on Title presence

Output:
  [
    SearchQuery { Artist: "Daft Punk", Title: "Get Lucky", ... },
    SearchQuery { Artist: "The Weeknd", Title: "Blinding Lights", ... }
  ]
```

---

## Design Principles

1. **Separation of Concerns**
   - UI doesn't talk to Soulseek directly
   - Business logic in services
   - Configuration separate from runtime

2. **Dependency Injection**
   - Services injected, not created
   - Loose coupling between components
   - Easy to test and extend

3. **Async Throughout**
   - No blocking calls on UI thread
   - Long operations in background
   - Responsive UI at all times

4. **Reactive Updates**
   - Event bus for major events
   - ObservableCollection for lists
   - INotifyPropertyChanged for properties

5. **Extensibility**
   - IInputSource for new input types
   - FileCondition for new filters
   - Services can be swapped

This architecture makes the codebase maintainable, testable, and ready for future enhancements.
