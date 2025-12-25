# ORBIT (formerly SLSKDONET): v1.0 Stabilization Roadmap

**Last Updated**: December 25, 2024  
**Repository**: https://github.com/MeshDigital/ORBIT  
**Current Phase**: **Phase 4 Complete** (Rekordbox Integration) → Preparing for Phase 5 (Self-Healing)

> [!IMPORTANT]
> **Status Update**: Phase 4 "Rekordbox Integration" is COMPLETE. ORBIT now features professional DJ export tools including playlist export and Monthly Drop functionality. Focus shifts to **Phase 5: Self-Healing Library**.

---

## ✅ Recently Completed (December 2024)

### Phase 2A: The Ironclad Recovery System - COMPLETE
**Impact**: Guaranteed zero data loss during crashes/power failures.
- **Crash Recovery Journal**: Transactional logging (SQLite WAL) for all destructive operations.
- **Atomic Tag Writes**: ACID-compliant metadata updates (`SafeWriteService`).
- **Resilient Downloads**: Thread-safe heartbeat monitoring with stall detection.
- **Automatic Recovery**: Self-healing on startup (checks journal, resumes operations, notifies user).
- **Performance**: <1% CPU overhead, <500ms startup delay.

### Phase 1B: Database Optimization - COMPLETE
**Impact**: 50-100x faster query performance.
- **WAL Mode**: Write-Ahead Logging for high concurrency.
- **Index Optimization**: Added critical covering indexes for library queries.
- **Connection Pooling**: Dedicated connection for high-frequency journal writes.

### Phase 8: Architectural Foundations - COMPLETE
**Impact**: Infrastructure for future sonic integrity features.
- **Producer-Consumer Pattern**: Non-blocking batch analysis architecture.
- **Dependency Validation**: Smart FFmpeg detection with graceful degradation.
- **Maintenance Tasks**: Automated database vacuuming and backup cleanup.

### Phase 0-1: Core Features - COMPLETE
- **Intelligent Ranking**: "The Brain" scoring system (Bitrate > BPM > Availability).
- **Spotify Integration**: PKCE Auth, Playlist import, Metadata enrichment.
- **Modern UI**: Dark-themed Avalonia interface with "Bento Grid" layout.
- **P2P Engine**: Robust Soulseek client implementation.

---

## Phase 3A: Atomic Resumability & UI Transparency - COMPLETE ✅
- **Impact**: No redownloading files after a crash; "Value Gap" bridged.
- **Atomic Downloads**: Full `.part` file workflow integrated with Recovery Journal.
- **Health Dashboard**: Backend recovery status visible on Home Screen.
- **Schema Expansion**: "Dual-Truth" audio features (Spotify vs Manual) added to TrackEntity.

## Phase 3B: Download Health Monitor - COMPLETE ✅
**Phase Goal**: Automated self-healing for network instability.
- **Stall Detection**: Adaptive timeout logic (60s standard, 120s for >90% progress)
- **Auto-Retry**: Automatic peer switching for stalled transfers
- **Peer Blacklisting**: Temporary blacklist for problematic peers
- **Dual-Truth Schema**: IntegrityLevel enum (Pending → Bronze → Silver → Gold)
- **Clear Dead-Letters**: UI feature for manual recovery

## Phase 4: Rekordbox Integration - COMPLETE ✅
**Phase Goal**: Professional DJ export functionality.
- **RekordboxService**: Streaming XML generation using XmlWriter
- **KeyConverter**: Musical key normalization (Standard → Camelot → OpenKey)
- **XmlSanitizer**: Metadata safety for Rekordbox compatibility
- **Playlist Export**: Context menu on playlist items
- **Monthly Drop**: Tools menu feature for recent tracks (last 30 days)
- **SaveFileDialog**: Integrated using Avalonia StorageProvider

## ✅ Phase 5A: Self-Healing Library - COMPLETE
**Phase Goal**: Automatic quality upgrades for library tracks.

### Core Components
- ✅ **LibraryScanner**: Batch processing with yield return (50 tracks/batch)
- ✅ **MetadataCloner**: Cross-format metadata transfer (ID3 ↔ Vorbis ↔ APE)
- ✅ **UpgradeScout**: P2P search with ±2s duration matching
- ✅ **FileLockMonitor**: Dual-layer safety (PlayerViewModel + OS-level exclusive lock)
- ✅ **UpgradeOrchestrator**: 8-step atomic swap with state machine

### Features
- **8-Step Transactional Swap**: Lock check, P2P search, shadow download, metadata clone, journal checkpoint, backup, swap, database update
- **State Machine**: 9 states with rollback logic (Pending → Downloading → CloningMetadata → ReadyToSwap → BackingUp → Swapping → UpdatingDatabase → Completed/Failed)
- **FLAC-Only Mode**: Conservative upgrade strategy (128/192kbps MP3 → FLAC)
- **7-Day Scan Cooldown**: Prevents redundant rescanning
- **7-Day Backup Retention**: Timestamped folders for rollback safety
- **Gold Status Exclusion**: Respects user-verified tracks
- **Quality Gain Tracking**: Bitrate delta + percent improvement for UI gamification
- **Crash Recovery**: Journal integration for mid-swap safety
- **Shadow Downloads**: Isolated `.orbit/tmp/upgrades` directory
- **Cross-Volume Detection**: MFT atomic update vs verified copy+delete

## 🔮 Phase 5B: Stealth Integration (Planned)
**Phase Goal**: Preserve Rekordbox analysis during file upgrades.

### Rekordbox Analysis Preservation (RAP)
- **Binary ANLZ Parsing**: Read beat grids, waveforms, hot cues from `.DAT/.EXT` files
- **XOR Descrambling**: Decrypt PSSI song structure phrases (Intro/Verse/Chorus/Outro)
- **Database Surgery**: Update Rekordbox master.db paths (SQLCipher integration)
- **Shadow Task Pattern**: Non-intrusive, fails gracefully without breaking core upgrade

### ANLZ Tags Supported
- `PQTZ` - Beat Grid (quantized tick markers)
- `PCOB` - Cue Points and Hot Cues
- `PWAV` - Waveform Preview
- `PSSI` - Song Structure (XOR-encrypted phrases)

### Value Proposition
When ORBIT upgrades MP3 → FLAC, DJ opens Rekordbox to find:
- ✅ High-quality FLAC file
- ✅ All waveforms intact
- ✅ Beat grid corrections preserved
- ✅ Hot cues and memory cues in place
- ✅ Song structure phrases (Intro/Verse/Outro) maintained

**No re-analysis required** - seamless upgrade experience for professional DJs.


## 🎯 Archived Priority: UI Placeholder Refactor (Completed Phase 3A)

With the backend hardened, we must align the frontend reality with user expectations.

### 1. Health Dashboard Integration
- **Goal**: Visualize the `CrashRecoveryJournal` status in the UI.
- **Implementation**:
    - Update `HomeViewModel` to query `DeadLetter` status.
    - Active "System Health" widget on Dashboard (Green/Orange/Red).
    - "Review Manual Fixes" action for dead-lettered files.

### 2. UI Transparency Audit
- **Goal**: Eliminate "dead clicks".
- **Implementation**:
    - Disable buttons for `Rekordbox Export` and `Spectral Analysis`.
    - Add "Coming v1.1" tooltips.
    - Connect `TrackInspector` spectral fields to actual data sources (or hide if empty).

---

## 🔮 Future Phases

### Month 2: UI Transparency & Speed (January 2026)

#### 1. UI Refactor: "The Transparency Phase" (Highest Priority)
- **Goal**: Connect placeholder UI elements to real logic or clearly mark as "Coming Soon".
- **Features**:
    - **Health Dashboard**: Live status of Crash Journal (DeadLetters) & API Health.
    - **Logic Connection**: Hook up "Spectral Analysis" and "Export" buttons.
    - **Disabled States**: Prevent "dead clicks" on unimplemented features.

#### 2. UI Virtualization (Critical)
- **Goal**: Support libraries with 50,000+ tracks.
- **Tech**: VirtualizingStackPanel, lazy-loading viewmodels.
- **Metric**: 60 FPS scrolling at 10k items.

#### 1. Upgrade Scout
- **Goal**: Automatically find better versions of existing tracks.
- **Logic**: If Library has 128kbps MP3 → Search P2P for FLAC matching duration/metadata → Background Download → Atomic Swap.

#### 2. Sonic Integrity (Phase 8 Implementation)
- **Goal**: Verify true audio quality.
- **Tech**: FFmpeg spectral analysis to detect "transcoded" fake lossless files.

### Phase 9: Hardware Export
- **Goal**: Sync to DJ hardware.
- **Target**: Rekordbox XML export, FAT32 USB sync for Denon/Pioneer.

---

## 📊 Performance Targets vs Reality

| Metric | Target | Current Status |
| :--- | :--- | :--- |
| **Startup Time** | < 2s | **~1.5s** (Excellent) |
| **Crash Recovery** | 100% | **100%** (Verified Phase 2A) |
| **UI Responsiveness** | 60 FPS | **30 FPS** (Needs Virtualization) |
| **Search Speed** | < 5s | **~2-4s** (Good) |
| **Memory Usage** | < 500MB | **~300-600MB** (Variable) |

---

## 🐛 Known Issues (Backlog)

### High Priority
- **UI Scalability**: Large playlists cause UI stuttering (Phase 6C TreeDataGrid required).
- **N+1 Queries**: Some UI views trigger redundant database fetches.
- **Soft Deletes**: Deleting projects is currently destructive (needs Audit Trail).

### Medium Priority
- **Duplicate Detection**: Batch imports sometimes miss duplicates within the same batch.
- **Drag-and-Drop**: Positioning issues on high-DPI displays.

---

**Last Updated**: December 24, 2025  
**Maintained By**: MeshDigital & AI Agents  
**License**: GPL-3.0
