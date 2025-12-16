# SLSKDONET: Current Status & Roadmap

## ✅ Completed Features

### Core Infrastructure
- **Persistence Layer**: SQLite database with Entity Framework Core
- **Download Management**: Concurrent downloads with progress tracking
- **Library System**: Playlist management with drag-and-drop organization
- **Audio Playback**: Built-in player with LibVLC integration
- **Import System**: Multi-source imports (Spotify, CSV, manual)
- **File Path Resolution** ✨: Advanced fuzzy matching with Levenshtein distance algorithm
  - Multi-step resolution: Fast check → Filename search → Fuzzy metadata matching
  - Configurable thresholds and library root paths
  - Database tracking with OriginalFilePath and FilePathUpdatedAt fields
  - See `DOCS/FILE_PATH_RESOLUTION.md` for details

### User Experience
- **Modern UI**: Dark-themed WPF interface with WPF-UI controls
- **Drag-and-Drop**: Visual playlist organization with adorners
- **Console Diagnostics**: Debug mode with detailed logging
- **Version Display**: Application version shown in status bar
- **Responsive Design**: Async operations keep UI responsive

### Technical Achievements
- **Database Concurrency**: Proper entity state management
- **UI Refresh**: Real-time updates from database
- **File Path Resolution**: Smart lookup from DownloadManager
- **Error Handling**: Comprehensive diagnostics and user feedback
- **Architecture**: Decoupled ViewModels with Coordinator pattern (86% code reduction)

---

## 🚧 In Progress

### Album Downloading
**Status**: Partial implementation
- Directory enumeration exists in `SoulseekAdapter`
- Needs UI grouping and batch download logic
- **Priority**: High

### Search Ranking
**Status**: Implemented but needs refinement
- Basic ranking system in place
- Could benefit from user feedback tuning
- **Priority**: Medium

---

## 🎯 Planned Features

### High Priority

#### 1. Spotify Metadata Foundation ✨ (Antigravity Ignition)
**The Gravity Well That Stabilizes Everything**

- **Database Schema**: Add Spotify IDs (track/album/artist) to all entities
- **Metadata Service**: Automatic enrichment with artwork, genres, popularity
- **Import Integration**: Every import gets canonical metadata anchors
- **Cache Layer**: Local metadata cache to avoid API spam
- **Impact**: **CRITICAL** - Foundation for all future features. Building this first avoids 12+ hours of rework later.

> **Why First**: Adding Spotify IDs later requires database migrations, import refactoring, and metadata backfill for existing tracks. Build the gravity well now, everything else orbits around it.

#### 2. Spotify OAuth Authentication (Anchorless Beacon)
- User sign-in with Spotify (PKCE flow) ✅ Core complete
- Access private playlists and collections
- Saved/liked tracks import
- Secure cross-platform token storage ✅ Complete
- **Status**: 40% complete (auth core done, UI pending)
- **Impact**: Unlocks user's entire Spotify library

#### 3. Critical Bug Fixes (Orbit Correction)
- Fix drag-drop namespace issue (build error)
- Implement Open Folder command
- Replace WPF dialogs with Avalonia
- Complete album download logic
- **Impact**: Stability and compilation

#### 2. Album Download Completion
- Recursive directory parsing for album mode
- UI grouping by album in Library view
- Batch download job management
- **Impact**: Major feature gap

#### 3. Metadata Enrichment
- Album art fetching (Last.fm/Spotify API)
- Automatic ID3 tag writing
- Cover art display in Library
- **Impact**: Visual polish

#### 4. Download Resume
- Partial file recovery after crashes
- Resume interrupted downloads
- Better error recovery
- **Impact**: Reliability

### Medium Priority

#### 4. Advanced Filters
- Bitrate range sliders
- Format multi-select
- Length tolerance
- User/uploader filters
- **Impact**: Power user features

#### 5. Playlist Export
- Export to M3U/M3U8
- Export to CSV
- Spotify playlist sync
- **Impact**: Workflow integration

#### 6. Batch Operations
- Multi-select in Library
- Bulk delete/move
- Batch metadata editing
- **Impact**: Efficiency

### Low Priority / Future

#### 7. Wishlist/Auto-Download
- Background monitoring for new releases
- Auto-queue matching tracks
- Notification system
- **Impact**: Automation

#### 8. Statistics Dashboard
- Download history charts
- Library analytics
- Source statistics
- **Impact**: Nice-to-have

#### 9. Themes
- Light mode option
- Custom color schemes
- User-defined themes
- **Impact**: Personalization

---

## 🐛 Known Issues

### Critical
- None currently

### Minor
- Drag-and-drop adorner positioning on high-DPI displays
- Occasional UI thread delays with large playlists (10k+ tracks)

---

## 📊 Performance Targets

- **Startup Time**: < 2 seconds
- **Search Response**: < 5 seconds for 100 results
- **Download Throughput**: Limited by network and Soulseek peers
- **UI Responsiveness**: No freezes during background operations
- **Database Operations**: < 100ms for typical queries

---

## 🔄 Recent Changes (v1.0.0)

- ✅ Fixed database concurrency exception in drag-and-drop
- ✅ Added UI refresh after playlist modifications
- ✅ Implemented file path resolution from DownloadManager
- ✅ Added taskbar icon with transparent background
- ✅ Enabled console diagnostics for debug builds
- ✅ Added version display in status bar

---

## 📝 Next Immediate Actions

1. **Implement Spotify OAuth (PKCE)** - Enable private playlist access
2. **Complete album downloading** - Highest user impact
3. **Add metadata/cover art** - Visual polish
4. **Implement download resume** - Reliability improvement
5. **Performance optimization** - Handle larger libraries
6. **User documentation** - Tutorials and guides

---

**Last Updated**: December 13, 2024
**Current Version**: 1.0.0
**Status**: Active Development
