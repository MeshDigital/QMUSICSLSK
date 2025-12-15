# SLSKDONET: Roadmap to v1.0

This roadmap outlines the strategic steps to elevate SLSKDONET from a functional prototype to a robust, "daily driver" application.

## 0. File Path Resolution (Priority: 🟢 Completed)
**Current State**: **Implemented**. Advanced file path resolution with fuzzy matching is live.
**What Was Implemented**:
- [x] **Levenshtein Distance Algorithm**: Fuzzy string matching utility for finding similar filenames.
- [x] **Multi-Step Resolution**: Fast check → Filename search → Fuzzy metadata matching.
- [x] **Configuration**: LibraryRootPaths, EnableFilePathResolution, FuzzyMatchThreshold settings.
- [x] **Database Tracking**: OriginalFilePath and FilePathUpdatedAt fields for audit trail.
- [x] **Documentation**: Comprehensive guide in `DOCS/FILE_PATH_RESOLUTION.md`.

## 1. The Stability Layer: Persistence (Priority: 🟢 Completed)
**Current State**: **Implemented**. SQLite integration is live. Library and Download Manager share a unified persistence layer.
**The Plan**:
- [x] **Database**: Integrate **SQLite** (lightweight, zero-config).
- [x] **Migration**: Move `PlaylistTrackViewModel` state to persistent storage.
- [x] **Lifecycle**: On app launch, restore all `Pending`, `Paused`, and `Failed` tracks.
- [x] **History**: Keep a log of `Completed` downloads for user reference.

## 2. The Missing Core: True Album Downloading (Priority: 🟠 High)
**Current State**: `SoulseekAdapter` finds album directories but doesn't process them. Users must pick tracks individually.
**The Plan**:
- **Directory Parsing**: Implement recursive file enumeration for `DownloadMode.Album`.
- **Grouping**: Update `LibraryViewModel` to group tracks by `Album` header.
- **Batch Logic**: Create `AlbumDownloadJob` that acts as a parent task for multiple files.

## 3. The Visual Experience: UI Polish (Priority: 🟡 Medium)
**Current State**: Functional, data-dense, text-heavy.
**The Plan**:
- **Album Art**: Integrate a metadata provider (e.g., Last.fm or Spotify API) to fetch cover art based on Artist/Album tags.
- **Styling**: Implement a consistent Design System (Colors, Typography) using `WPF UI` or modern styles.
- **Feedback**: Add toast notifications for "Download Complete" or "Search Finished".

## 4. Automation: The "Wishlist" (Priority: 🟢 Low/Future)
**Current State**: Manual Search -> Download flow.
**The Plan**:
- **Wishlist**: Users add "Artist - Album" to a watch list.
- **Sentinel**: A low-priority background worker searches periodically (e.g., every 30 mins).
- **Auto-Snatch**: Automatically queues items that meet strict criteria (e.g., 320kbps + Free Slot).

## 5. Performance Improvements
- **Virtualization**: Ensure `DataGrid` enables UI virtualization to handle 10,000+ items without lag.
- **Memory Management**: Optimize `ObservableCollection` updates (using `AddRange` patterns) to reduce UI thread thrashing.

---

## Recommended Next Step: 1. Persistence
Implementing SQLite persistence is the most high-impact change for user trust. It transforms the app from a "session tool" to a "library manager".
