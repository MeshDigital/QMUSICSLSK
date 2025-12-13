# SLSKDONET – Soulseek Music Downloader

A modern Windows desktop application for orchestrating music downloads from the Soulseek network. Import playlists from Spotify or CSV, manage downloads with a visual queue, organize your library, and play your music—all in one place.

## ✨ Key Features

- **Multi-Source Import**: Import from Spotify playlists, CSV files, or manual queries
- **Smart Download Management**: Concurrent downloads with progress tracking and automatic retries
- **Library Organization**: Drag-and-drop playlist management with SQLite persistence
- **Built-in Audio Player**: Play your downloaded tracks with LibVLC integration
- **Console Diagnostics**: Debug mode with detailed logging (no Visual Studio required)
- **Modern UI**: Clean, dark-themed interface with WPF-UI controls

## 🚀 Quick Start

### Prerequisites
- Windows 10/11
- .NET SDK 8.0+
- Soulseek account (free at [slsknet.org](https://www.slsknet.org))

### Build & Run

```powershell
# Clone and build
git clone <repository-url>
cd QMUSICSLSK
dotnet restore
dotnet build

# Run the application
dotnet run --project SLSKDONET.csproj
```

### First-Time Setup

1. **Sign in** with your Soulseek credentials (stored securely)
2. **Configure download directory** in Settings
3. **Import music** from Spotify, CSV, or manual search
4. **Start downloading** and monitor progress in Library view

## 📖 Typical Workflow

1. **Import** → Add tracks from Spotify playlist or CSV file
2. **Review** → Preview imported tracks in the import dialog
3. **Download** → Tracks are queued and downloaded automatically
4. **Organize** → Drag tracks between playlists in Library view
5. **Play** → Double-click or drag tracks to the built-in player

## 🎵 Audio Playback

The application includes a built-in audio player powered by LibVLC:
- Play downloaded tracks directly from the Library
- Drag-and-drop tracks to the player sidebar
- Supports MP3, FLAC, and other common formats

**Note**: LibVLC native libraries are included automatically. If playback fails, check the console output for diagnostic messages.

## 🐛 Diagnostics & Troubleshooting

### Debug Mode (Console Output)

Debug builds show a console window with detailed diagnostics:

```powershell
# Build in Debug mode
dotnet build --configuration Debug

# Run and view console output
cd bin\Debug\net8.0-windows
.\SLSKDONET.exe
```

The console displays:
- `[DRAG]` - Drag-and-drop operations
- `[PLAYBACK]` - Audio player events
- `info/warn/fail` - Service-level diagnostics

### Common Issues

**Audio playback fails**:
- Check console for LibVLC initialization errors
- Ensure `libvlc` folder exists in output directory
- Player sidebar shows "Player Initialization Failed" if libraries are missing

**Drag-and-drop not working**:
- Console shows detailed drag events for troubleshooting
- Check for `AdornerLayer` warnings in console output

**Tracks don't appear after drag-and-drop**:
- Fixed in latest version - tracks now reload from database immediately

## 📁 Configuration

Configuration is stored in `%AppData%\SLSKDONET\config.ini`:

```ini
[Soulseek]
Username = your-username
Password = your-password
Server = server.slsknet.org
Port = 2242

[Download]
Directory = C:\Users\you\Music\SLSKDONET
MaxConcurrentDownloads = 2
NameFormat = {artist} - {title}
PreferredFormats = mp3,flac
```

## 🏗️ Architecture

- **UI Layer**: WPF with MVVM pattern, WPF-UI controls
- **Services**: Download orchestration, Soulseek adapter, audio player
- **Persistence**: SQLite via Entity Framework Core
- **Import Providers**: Spotify API, CSV parser, manual input

See [`ARCHITECTURE.md`](ARCHITECTURE.md) for detailed system design.

## 📚 Documentation

- [`ARCHITECTURE.md`](ARCHITECTURE.md) - System architecture and data flows
- [`FEATURES.md`](FEATURES.md) - Complete feature list
- [`TROUBLESHOOTING.md`](TROUBLESHOOTING.md) - Common issues and solutions
- [`DEVELOPMENT.md`](DEVELOPMENT.md) - Developer setup and contribution guide

## 🔒 Security

- Passwords encrypted using Windows Data Protection API (DPAPI)
- No credentials stored in plain text
- SQLite database stored locally in user's AppData folder

## 📝 License

GPL-3.0

---

**Version**: 1.0.0 | **Status**: Active Development
