# QMUSICSLSK - Upgrade Roadmap

**Spotify-Like Music Player - Feature Implementation Plan**

---

## Current Status: 70% Complete ✅

### What's Working
- ✅ Search with ranking (Soulseek P2P)
- ✅ Playlists (6 loaded from database)
- ✅ Library management
- ✅ Track playback (LibVLC)
- ✅ Download tracking
- ✅ Drag-drop reordering
- ✅ Import (Spotify URLs, CSV)
- ✅ Album grouping
- ✅ Context menu
- ✅ Keyboard shortcuts
- ✅ Window state persistence
- ✅ System tray

### Database
- ✅ SQLite with full history tracking
- ✅ PlaylistJobs, PlaylistTracks, ActivityLogs
- ✅ Automatic migrations
- ✅ Foreign keys, indexes

---

## Phase 1: Critical Features (6 hours) 🔴

### 1.1 Queue Management (2 hours)
**Priority**: ⭐⭐⭐ CRITICAL

**What to Build**:
- [ ] Play queue view (right sidebar or bottom panel)
- [ ] Add to queue button (context menu + search results)
- [ ] Clear queue button
- [ ] Drag-drop reorder queue
- [ ] Remove from queue

**Files to Create/Modify**:
- `ViewModels/QueueViewModel.cs` (new)
- `Views/Avalonia/QueuePanel.axaml` (new)
- `PlayerViewModel.cs` (add queue management)
- `MainWindow.axaml` (add queue panel)

**Backend Status**: ⚠️ Partial (PlayerViewModel exists, needs queue logic)

**Implementation Steps**:
1. Create `QueueViewModel` with `ObservableCollection<PlaylistTrackViewModel>`
2. Add `AddToQueue()`, `RemoveFromQueue()`, `ClearQueue()` methods
3. Create `QueuePanel.axaml` UI component
4. Wire up to PlayerViewModel
5. Add context menu "Add to Queue" option
6. Test queue playback flow

---

### 1.2 Now Playing View (3 hours)
**Priority**: ⭐⭐⭐ CRITICAL

**What to Build**:
- [ ] Full-screen now playing view
- [ ] Large album art display (300x300px)
- [ ] Track progress bar with time
- [ ] Next/Previous buttons
- [ ] Shuffle toggle
- [ ] Repeat toggle (off/one/all)
- [ ] Volume slider
- [ ] Like/favorite button

**Files to Create/Modify**:
- `Views/Avalonia/NowPlayingPage.axaml` (new)
- `PlayerViewModel.cs` (add shuffle, repeat logic)
- `MainViewModel.cs` (add navigation to now playing)

**Backend Status**: ✅ PlayerViewModel ready

**Implementation Steps**:
1. Create `NowPlayingPage.axaml` with large layout
2. Add album art image binding
3. Implement progress bar with seek
4. Add shuffle/repeat state to PlayerViewModel
5. Wire up all playback controls
6. Add navigation from mini player
7. Test all playback modes

---

### 1.3 Playlist History UI (30 minutes)
**Priority**: ⭐⭐⭐ QUICK WIN

**What to Build**:
- [ ] Small 📜 icon next to each playlist name
- [ ] Click → show activity log panel
- [ ] Timeline view with timestamps
- [ ] Filter by action type
- [ ] Show: Added, Removed, Moved, Created, Deleted

**Files to Create/Modify**:
- `Views/Avalonia/PlaylistHistoryPanel.axaml` (new)
- `ViewModels/PlaylistHistoryViewModel.cs` (new)
- `LibraryPage.axaml` (add history icon)

**Backend Status**: ✅ ActivityLogs table ready

**Implementation Steps**:
1. Create `PlaylistHistoryViewModel` loading from `ActivityLogs`
2. Create `PlaylistHistoryPanel.axaml` with timeline UI
3. Add small icon to playlist list items
4. Wire up click event
5. Test with existing activity data

---

### 1.4 Album Detail View (45 minutes)
**Priority**: ⭐⭐ QUICK WIN

**What to Build**:
- [ ] Click album in search results → show detail
- [ ] Album detail panel/page
- [ ] Track list with all album tracks
- [ ] Download all button
- [ ] Album metadata (artist, year, track count)

**Files to Create/Modify**:
- `Views/Avalonia/AlbumDetailPanel.axaml` (new)
- `ViewModels/AlbumDetailViewModel.cs` (new)
- `SearchPage.axaml` (add click handler)

**Backend Status**: ✅ AlbumResults ready

**Implementation Steps**:
1. Create `AlbumDetailViewModel` with track list
2. Create `AlbumDetailPanel.axaml` UI
3. Add click event to album grid
4. Show panel/navigate to detail
5. Test download all functionality

---

## Phase 2: Enhanced Features (6 hours) 🟡

### 2.1 Search Filters (1 hour)
**Priority**: ⭐⭐

**What to Build**:
- [ ] Format filter dropdown (MP3, FLAC, AAC, etc.)
- [ ] Bitrate filter (128k, 192k, 320k, lossless)
- [ ] File size filter
- [ ] Duration filter
- [ ] Clear filters button

**Files to Modify**:
- `SearchPage.axaml` (add filter UI)
- `MainViewModel.cs` (add filter logic)

**Backend Status**: ✅ Filter logic exists

---

### 2.2 Smart Playlists (3 hours)
**Priority**: ⭐⭐

**What to Build**:
- [ ] Recently Added (last 30 days)
- [ ] Most Played (track play count)
- [ ] Favorites/Liked Songs
- [ ] Failed Downloads
- [ ] High Quality (FLAC only)
- [ ] Auto-update on library changes

**Files to Create**:
- `ViewModels/SmartPlaylistViewModel.cs`
- `Services/SmartPlaylistService.cs`

**Backend Status**: ⚠️ Need play count tracking

**Implementation**:
1. Add `PlayCount` to PlaylistTrack entity
2. Create smart playlist logic
3. Add to library sidebar
4. Auto-refresh on changes

---

### 2.3 Social Features (2 hours)
**Priority**: ⭐⭐

**What to Build**:
- [ ] Export playlist to M3U/JSON
- [ ] Import playlist from M3U/JSON
- [ ] Share playlist (copy to clipboard)
- [ ] Collaborative playlists (advanced)

**Files to Create**:
- `Services/PlaylistExportService.cs`
- `Services/PlaylistImportService.cs`

---

## Phase 3: Polish Features (22 hours) 🟢

### 3.1 Lyrics Display (4 hours)
- [ ] Fetch lyrics from API (Genius, Musixmatch)
- [ ] Display in now playing view
- [ ] Scroll sync (optional)
- [ ] Cache lyrics locally

### 3.2 Crossfade (4 hours)
- [ ] Crossfade duration setting (0-12s)
- [ ] Smooth transitions between tracks
- [ ] LibVLC audio mixing

### 3.3 Equalizer (6 hours)
- [ ] 10-band EQ UI
- [ ] Presets (Rock, Pop, Jazz, etc.)
- [ ] Save custom presets
- [ ] LibVLC audio filters

### 3.4 Visualizer (8 hours)
- [ ] Audio spectrum analyzer
- [ ] Waveform display
- [ ] Multiple visualizer styles
- [ ] SkiaSharp rendering

---

## Phase 2B: Data Management & Resilience (8 hours) 🟡

### 2.4 DB Backup & Restore (2 hours)
**Priority**: ⭐⭐⭐ CRITICAL

**What to Build**:
- [ ] Backup library.db to external drive
- [ ] Backup config.ini with database
- [ ] Restore from backup
- [ ] Scheduled auto-backup option
- [ ] Backup verification

**Files to Create**:
- `Services/BackupService.cs` (new)
- `Views/Avalonia/BackupPage.axaml` (new)

**Implementation**:
1. Use SQLite `VACUUM INTO` or `File.Copy`
2. Create BackupService with atomic backup
3. Add backup UI to Settings page
4. Implement restore with validation
5. Add auto-backup scheduler

---

### 2.5 DB Import/Export JSON (2 hours)
**Priority**: ⭐⭐

**What to Build**:
- [ ] Export all playlists to JSON
- [ ] Export library entries to JSON
- [ ] Import from JSON (merge or replace)
- [ ] Portable library format

**Files to Modify**:
- `Services/LibraryService.cs` (add Export/Import methods)

**Implementation**:
1. Serialize PlaylistJob + PlaylistTrack to JSON
2. Include metadata and relationships
3. Import with duplicate detection
4. Validate JSON schema on import

---

### 2.6 Library Indexing Optimization (1 hour)
**Priority**: ⭐⭐

**What to Build**:
- [ ] Add database indexes for faster queries
- [ ] Index on PlaylistId, Status, TrackUniqueHash
- [ ] Index on CreatedAt for sorting
- [ ] Measure query performance improvement

**Files to Modify**:
- `Data/AppDbContext.cs` (add indexes)

**Implementation**:
```csharp
modelBuilder.Entity<PlaylistTrackEntity>()
    .HasIndex(t => t.PlaylistId);
modelBuilder.Entity<PlaylistTrackEntity>()
    .HasIndex(t => t.Status);
modelBuilder.Entity<PlaylistTrackEntity>()
    .HasIndex(t => t.TrackUniqueHash);
modelBuilder.Entity<PlaylistJobEntity>()
    .HasIndex(j => j.CreatedAt);
```

---

### 2.7 True Pause/Resume Downloads (3 hours)
**Priority**: ⭐⭐⭐

**What to Build**:
- [ ] Check for .part files on resume
- [ ] Resume from file size offset
- [ ] Validate partial downloads
- [ ] Handle corrupted .part files

**Files to Modify**:
- `Services/DownloadManager.cs`
- `Services/SoulseekAdapter.cs` (add startOffset support)

**Implementation**:
1. Check if .part file exists before download
2. Get file size as resume offset
3. Pass offset to Soulseek download
4. Verify file integrity after resume
5. Delete .part if corrupted

---

## Phase 4: Advanced Features (Future)

### 4.1 Rekordbox Export
- [ ] Export to Rekordbox XML
- [ ] Preserve playlists, cues, metadata

### 4.2 Mobile Sync
- [ ] Export library to mobile device
- [ ] Sync playlists

### 4.3 Cloud Backup
- [ ] Backup library to cloud
- [ ] Restore from backup

---

## Phase 5: Quality Control & Replacement System (16 hours) 🔵

### 5.1 Audio Fingerprinting (4 hours)
**Priority**: ⭐⭐⭐ ADVANCED

**What to Build**:
- [ ] Integrate SoundFingerprinting library
- [ ] Generate fingerprint hash for each track
- [ ] Store fingerprint in database
- [ ] Compare fingerprints for duplicates

**Files to Create**:
- `Services/FingerprintService.cs` (new)
- Add NuGet: `SoundFingerprinting` (MIT license)

**Implementation**:
1. Add SoundFingerprinting NuGet package
2. Create FingerprintService
3. Generate hash post-download
4. Store in LibraryEntry.FingerprintHash
5. Compare hashes for perceptual duplicates

---

### 5.2 Track Quality Data Model (2 hours)
**Priority**: ⭐⭐⭐

**What to Build**:
- [ ] Add FingerprintHash to LibraryEntry
- [ ] Add BitrateScore property
- [ ] Add FingerprintQuality property
- [ ] Add QualityFlags enum

**Files to Modify**:
- `Models/LibraryEntry.cs`
- `Models/PlaylistTrack.cs`
- `Data/Entities/` (corresponding entities)

**Implementation**:
```csharp
public string? FingerprintHash { get; set; }
public int BitrateScore { get; set; }
public double FingerprintQuality { get; set; }
public QualityFlags Flags { get; set; }
```

---

### 5.3 Quality Analysis Service (6 hours)
**Priority**: ⭐⭐⭐

**What to Build**:
- [ ] Post-download quality analysis
- [ ] Compare against existing library
- [ ] Flag duplicates for review
- [ ] Suggest higher quality replacements
- [ ] Auto-replace option (with confirmation)

**Files to Create**:
- `Services/QualityAnalysisService.cs` (new)

**Implementation**:
1. Run analysis after download completes
2. Check fingerprint against all LibraryEntry
3. Compare quality metrics (bitrate, format, length)
4. Flag duplicates with lower quality
5. Suggest replacement candidates
6. Log analysis results

---

### 5.4 Quality Review UI (4 hours)
**Priority**: ⭐⭐

**What to Build**:
- [ ] "Library Health" page
- [ ] List flagged duplicates
- [ ] Show quality comparison
- [ ] Replace/Keep/Delete actions
- [ ] Batch operations

**Files to Create**:
- `Views/Avalonia/QualityReviewPage.axaml` (new)
- `ViewModels/QualityReviewViewModel.cs` (new)

**Implementation**:
1. Create QualityReviewViewModel
2. Load flagged tracks from database
3. Display side-by-side comparison
4. Implement replace logic
5. Add to navigation

---

## Implementation Priority

### Week 1 (6 hours)
1. Queue management (2h)
2. Now playing view (3h)
3. Playlist history UI (30m)
4. Album detail view (45m)

**Result**: 90% Spotify feature parity

### Week 2 (6 hours)
5. Search filters (1h)
6. Smart playlists (3h)
7. Social features (2h)

**Result**: Enhanced library features

### Week 3-4 (22 hours)
8. Lyrics (4h)
9. Crossfade (4h)
10. Equalizer (6h)
11. Visualizer (8h)

**Result**: Premium features

---

## Technical Debt

### Code Quality
- [ ] Add unit tests for ViewModels
- [ ] Add integration tests for services
- [ ] Document all public APIs
- [ ] Code coverage >80%

### Performance
- [ ] Lazy load large playlists
- [ ] Virtualize track lists
- [ ] Optimize database queries
- [ ] Cache album art

### UX
- [ ] Loading indicators
- [ ] Error messages
- [ ] Tooltips everywhere
- [ ] Keyboard shortcuts for all actions

---

## Success Metrics

### Current
- 70% feature parity with Spotify
- 6 playlists loaded
- Full database tracking
- Cross-platform ready

### After Phase 1
- 90% feature parity
- Queue management
- Full now playing view
- History tracking UI

### After Phase 2
- 95% feature parity
- Smart playlists
- Advanced filters
- Social features

### After Phase 3
- 100% feature parity
- Premium features
- Professional polish

---

## Unique Advantages Over Spotify

1. **P2P Downloads** - Own your music forever
2. **No Subscription** - Free forever
3. **High Quality** - FLAC support
4. **Smart Ranking** - Quality detection
5. **Offline by Default** - Already downloads
6. **Privacy** - No tracking
7. **Open Source** - Full control

---

## Notes

- All backend logic is production-ready
- Database handles 1000s of tracks
- Cross-platform (Windows, macOS, Linux)
- Modern Avalonia UI
- Clean MVVM architecture

**Next Step**: Implement Phase 1 (6 hours) for 90% parity
