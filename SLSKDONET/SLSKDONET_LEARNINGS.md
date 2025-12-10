# SLSKDONET Development - Learning from slsk-batchdl

## Analysis Summary

This document summarizes what we've learned from analyzing `slsk-batchdl` and what we're implementing in SLSKDONET.

---

## Key Learnings from slsk-batchdl

### 1. **Architecture Excellence**
- **Service-Oriented**: Each major function (search, download, filter, format) is a separate, composable service
- **Configuration-Driven**: Rich config system with profiles allows flexibility without code changes
- **Event Pipeline**: Filter → Rank → Download creates clear data flow
- **Extensibility**: Plugin architecture for input sources and conditions

**How We're Using It**:
- Created service layer with `SoulseekAdapter`, `DownloadManager`, `FileNameFormatter`, `FileConditionEvaluator`
- Building configuration system with profiles support (next phase)
- Event bus pattern in `SoulseekAdapter` for reactive updates

### 2. **Input Source Abstraction**
slsk-batchdl supports 7+ input types (CSV, Spotify, YouTube, Bandcamp, MusicBrainz, Lists, Soulseek links, search strings).

**How We're Using It**:
- Created `IInputSource` interface for pluggable input parsing
- Implemented `CsvInputSource`, `StringInputSource`, `ListInputSource`
- Auto-detection of column names in CSV (very user-friendly)
- Can easily add Spotify/YouTube support later

### 3. **Smart Search Query Building**
- Parses multiple input formats (Artist - Title, property=value, literals)
- Fallback search when artist is wrong
- Regex manipulation before searching
- Remove "feat." artists, cleanup YouTube titles, etc.

**How We're Using It**:
- `SearchQuery` class supports multiple parsing formats
- `SearchQueryNormalizer` handles text cleanup
- Extensible for more normalization rules

### 4. **Sophisticated File Filtering**
- **Two-tier system**: Required conditions (all must pass) + Preferred conditions (scored)
- Files passing more preferred conditions rank higher
- Graceful handling of missing metadata (doesn't reject)
- But can enforce strict conditions if needed

**How We're Using It**:
- `FileCondition` abstract class with subclasses: `FormatCondition`, `BitrateCondition`, `LengthCondition`, `SampleRateCondition`, `UserCondition`
- `FileConditionEvaluator` implements ranking algorithm
- Scores files on a 0-1 scale based on preference match

### 5. **Flexible File Naming**
- Template-based formatting: `{artist}/{album}/{track}. {title}`
- Fallback support: `{artist|filename}` uses filename if artist is null
- Literal wrapping: `{artist( - )title}` only adds " - " if artist exists
- Sanitizes invalid characters automatically

**How We're Using It**:
- `FileNameFormatter` with regex-based template parsing
- Supports variable interpolation and fallbacks
- Safe sanitization that works across platforms

### 6. **Download Mode Flexibility**
Four distinct modes that each require different logic:
- **Normal**: Single file per query (our current implementation)
- **Album**: Download entire folder
- **Aggregate**: Find all distinct songs by artist
- **Album Aggregate**: Combine both (useful for discovering all albums)

**How We're Using It**:
- Created `DownloadMode` enum
- Extended `SearchQuery` to include mode
- Next: Implement album/aggregate logic in `DownloadManager`

### 7. **Rate Limiting & Search Management**
- Prevents 30-minute Soulseek bans (34 searches per 220 seconds by default)
- Token bucket algorithm for search quota
- Queue-based search throttling
- Graceful "Waiting" status feedback

**How We'll Use It**:
- Plan to implement `SearchRateLimiter` service using token bucket pattern
- Track search quota and queue pending searches
- Show "Waiting for search quota" in UI

### 8. **Persistent State Management**
- Download index file tracks all completed/failed downloads
- Skip already-downloaded tracks
- Resume capability on failure
- Useful for playlists that update over time

**How We'll Use It**:
- Create `DownloadIndex` model (JSON-based)
- Implement `IndexManager` service
- Option to skip existing downloads

### 9. **Post-Download Actions**
- Run commands when download succeeds/fails
- Examples: Convert to MP3, fetch album art, update player
- Chainable actions with output capture
- Can update index based on conversion success

**How We'll Use It**:
- Create `OnCompleteActionExecutor` service
- Parse action syntax and prefixes
- Execute with environment variables substituted

### 10. **Interactive Album Mode**
- Display results ranked by availability
- Keyboard-driven UI for folder navigation
- Filter results in real-time
- Download specific files from a folder

**How We'll Use It**:
- Create `InteractiveDownloadWindow` WPF view
- Implement keyboard event handling
- Show result availability counts

---

## What We've Implemented (Phase 1)

### Models
✅ `Track` - Track metadata with helpers
✅ `DownloadJob` - Download state machine
✅ `DownloadState` - Enum for job states
✅ `SearchQuery` - Query parsing (multiple formats)
✅ `DownloadMode` - Enum for download modes
✅ `InputType` - Enum for input source types
✅ `FileCondition` - Abstract condition system
  - `FormatCondition`
  - `BitrateCondition`
  - `LengthCondition`
  - `SampleRateCondition`
  - `UserCondition`
✅ `FileConditionEvaluator` - Condition ranking

### Services
✅ `SoulseekAdapter` - Soulseek.NET wrapper
✅ `DownloadManager` - Download orchestration (basic)
✅ `FileNameFormatter` - Template-based naming
✅ `SearchQueryNormalizer` - Text cleanup

### Input Parsers
✅ `IInputSource` - Interface
✅ `CsvInputSource` - CSV parsing with auto-detection
✅ `StringInputSource` - Direct search strings
✅ `ListInputSource` - List file parsing

### Utilities
✅ `FileFormattingUtils` - Size/time formatting
✅ `ValidationUtils` - Input validation

### Views
✅ `MainWindow` - Basic WPF UI (search & downloads tabs)
✅ `MainViewModel` - MVVM binding and logic
✅ `App.xaml` - WPF resources
✅ `App.xaml.cs` - DI setup

### Configuration
✅ `AppConfig` - Settings model
✅ `ConfigManager` - INI file management

---

## What's Next (Phase 2)

### High Priority
- [ ] Album download mode implementation
- [ ] Configuration profiles with auto-selection
- [ ] Search rate limiting with token bucket
- [ ] Download index file management
- [ ] Enhanced error handling and logging

### Medium Priority
- [ ] Interactive album mode UI
- [ ] On-complete action executor
- [ ] Aggregate download mode
- [ ] Advanced search options (desperate mode, etc.)

### Lower Priority
- [ ] Spotify integration
- [ ] YouTube integration
- [ ] Bandcamp integration
- [ ] Unit tests
- [ ] Performance optimization

---

## Design Patterns Applied

### From slsk-batchdl

| Pattern | Location | Benefit |
|---------|----------|---------|
| Strategy Pattern | Input sources (`IInputSource`) | Pluggable input types |
| Decorator Pattern | File conditions | Composable filters |
| Observer Pattern | `SoulseekAdapter.EventBus` | Reactive UI updates |
| Factory Pattern | Input detection (upcoming) | Auto-select parser |
| Template Method | `FileNameFormatter` | Flexible naming |
| Chain of Responsibility | Condition evaluation | Layered filtering |
| Repository Pattern | `IndexManager` (upcoming) | Persistent state |

---

## Key Implementation Decisions

### Why MVVM for GUI?
- Clean separation of UI and logic
- Easy to test view models
- Reactive data binding
- Matches enterprise C# patterns

### Why Dependency Injection?
- Services are loosely coupled
- Easy to swap implementations (e.g., mock adapter for testing)
- Follows SOLID principles
- Industry standard in .NET

### Why Two-Tier Conditions?
- Required: Hard constraints (can't break these)
- Preferred: Soft preferences (for ranking)
- Most flexible approach
- Mirrors real-world download scenarios

### Why Reactive EventBus?
- Non-blocking event handling
- Compose event streams
- Testable
- Matches async/await model

---

## Code Comparison

### slsk-batchdl vs SLSKDONET

| Feature | slsk-batchdl | SLSKDONET |
|---------|-------------|-----------|
| UI | CLI with rich TUI | WPF GUI |
| Language | C# | C# |
| Architecture | Service-oriented | Service-oriented (same) |
| Config | INI with profiles | INI with profiles (implementing) |
| Input sources | 7+ types | 3+ types (easily extensible) |
| Conditions | Rich filtering | Two-tier system |
| Threading | Async/await | Async/await (same) |
| Testing | Mock files dir | Unit tests (planned) |

---

## Performance Considerations

From slsk-batchdl experience:
- **Concurrency**: Default 2-4 parallel downloads is safe
- **Search timing**: 34 searches per 220 seconds is Soulseek limit
- **Result count**: 100 results per search is reasonable
- **Timeout tuning**: 6s search, 20s connect, 30s stale time defaults

We match these defaults in `AppConfig`.

---

## Next Steps

1. **Complete Phase 1 build** (current)
2. **Test core functionality** - Connect, search, download
3. **Implement Phase 2 features** - Album mode, profiles, rate limiting
4. **Add extensive logging** - Debug info for troubleshooting
5. **Create installer** - NSIS or WiX for distribution
6. **Documentation** - User guide, dev guide (started)

---

## References

- **slsk-batchdl**: https://github.com/fiso64/slsk-batchdl
- **Soulseek.NET**: https://github.com/jpdillingham/Soulseek.NET
- **WPF MVVM Pattern**: https://docs.microsoft.com/en-us/windows/uwp/get-started/building-a-basic-universal-windows-app-with-xaml-and-c-sharp
- **System.Reactive**: https://github.com/ReactiveX/RxJS/wiki

