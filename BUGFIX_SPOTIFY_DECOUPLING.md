# Bug Fixes - December 21, 2025

## Issues Fixed

### 1. Spotify UI Bug - Settings Page
**Problem:** Connect/Disconnect buttons were unresponsive in Settings page.

**Root Cause:**
- `IsAuthenticating` state wasn't properly notifying both commands
- `CheckSpotifyConnectionStatusAsync()` was incorrectly setting `IsAuthenticating = true`
- `DisconnectSpotifyCommand` had no CanExecute check

**Files Changed:**
- `ViewModels/SettingsViewModel.cs`
  - Removed `IsAuthenticating` state management from `CheckSpotifyConnectionStatusAsync()`
  - Added `DisconnectSpotifyCommand` notification when `IsAuthenticating` changes
  - Added `CanExecute` check to `DisconnectSpotifyCommand`

### 2. Spotify Services Decoupling
**Problem:** App crashes when not logged into Spotify. Spotify integration interfered with core functionality.

**Root Cause:**
- Spotify services were making API calls without checking authentication status
- Enrichment processes failed when not authenticated
- No graceful fallback for unauthenticated state

**Files Changed:**
- `Services/SpotifyMetadataService.cs`
  - Added authentication check in `EnrichTrackAsync()` - returns false if not authenticated
  - Added authentication check in `FindTrackAsync()` - returns null if not authenticated
  - Added authentication check in `GetAudioFeaturesBatchAsync()` - returns empty results if not authenticated

- `Services/MetadataEnrichmentOrchestrator.cs`
  - Improved logging when skipping enrichment due to no auth
  - Properly cleans up pending orchestrations when not authenticated

- `ViewModels/ImportPreviewViewModel.cs`
  - Added safety check in `EnrichTracksInBackgroundAsync()` - exits early if metadata service unavailable

**Result:** App now functions completely without Spotify authentication. All enrichment processes gracefully skip when not logged in.

### 3. Build Errors Fixed

#### Missing TreeDataGrid Dependency
**Problem:** Build failed with error about missing `Avalonia.Controls.TreeDataGrid.csproj`

**Solution:**
- Added TreeDataGrid as git submodule from `https://github.com/AvaloniaUI/Avalonia.Controls.TreeDataGrid.git`
- Initialized submodule in `External/TreeDataGrid/`

#### Invalid XAML Property
**Problem:** Build error in `App.axaml` line 53 - `Height="Auto"` not valid format

**Solution:**
- Removed `<Setter Property="Height" Value="Auto"/>` from Button.nav-item style
- Buttons default to auto-size anyway

## Known Issues

### Library Not Functioning
**Problem:** Spotify import view redirect to import library (new playlist) crashes the app. Library needs revision.

**Status:** Open - requires investigation

## Testing Notes

- Build succeeds with only nullable reference warnings (no errors)
- App runs without Spotify authentication
- Settings page Spotify UI now responds correctly
- Enrichment processes skip gracefully when not authenticated

## Files Modified Summary

1. `ViewModels/SettingsViewModel.cs` - Spotify UI fixes
2. `Services/SpotifyMetadataService.cs` - Auth guards
3. `Services/MetadataEnrichmentOrchestrator.cs` - Auth handling
4. `ViewModels/ImportPreviewViewModel.cs` - Safety check
5. `App.axaml` - Removed invalid Height property
6. `.gitmodules` - Added TreeDataGrid submodule
7. `External/TreeDataGrid/` - Submodule added

## Commits
- Fix Spotify settings UI and decouple Spotify services from core functionality
