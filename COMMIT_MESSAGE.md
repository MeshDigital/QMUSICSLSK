feat: Library UI Polish, Event Bus Consolidation, and Spotify Enhancements

## Library UI Refinements
- **Smart Grouping**: Flatten single-track albums to avoid unnecessary nested views
- **Filter Mutual Exclusion**: "All", "Downloaded", "Pending" now behave like radio buttons
- **Fixed Album Download Regression**: Added `DownloadAlbumCommand` to `AlbumNode` and injected `DownloadManager` dependency chain

## Event Bus Consolidation
- Enhanced `TrackAddedEvent` record with optional `InitialState` parameter
- Removed duplicate event definitions from `Events/TrackEvents.cs`
- Standardized on `Models/Events.cs` for all event records
- Prevents "zombie pending" tracks during database hydration

## Spotify Enhancements
- Added `Scopes.UserTopRead` to authorization request
- Enables `GetRecommendations` feature (requires re-authentication)
- Fixed null safety in event references

## Technical Improvements
- Proper dependency injection for `AlbumNode` via `HierarchicalLibraryViewModel`
- Type-safe event records with C# record syntax
- Cleaner command bindings in XAML views
