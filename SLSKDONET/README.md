# SLSKDONET - Soulseek.NET Batch Downloader

A modern WPF desktop application for batch downloading music from Soulseek using the Soulseek.NET library.

## Features

- **Soulseek Integration**: Uses Soulseek.NET for network communication
- **Modern UI**: WPF-based graphical interface with search and download management
- **Batch Downloads**: Queue multiple files for concurrent downloading
- **Smart Filtering**: Filter search results by bitrate, format, and other metadata
- **Configuration Management**: Save and load user preferences
- **Progress Tracking**: Real-time download progress for all active transfers
- **Error Handling**: Robust error handling with detailed logging

## Architecture

### Project Structure

```
SLSKDONET/
├── Models/                 # Data models
│   ├── Track.cs           # Track information
│   └── DownloadJob.cs     # Download job state
├── Services/              # Business logic
│   ├── SoulseekAdapter.cs # Soulseek.NET wrapper
│   └── DownloadManager.cs # Download orchestration
├── Configuration/         # Config management
│   ├── AppConfig.cs       # Configuration model
│   └── ConfigManager.cs   # Config file handling
├── Views/                 # UI (WPF)
│   ├── MainWindow.xaml    # Main window UI
│   ├── MainWindow.xaml.cs # Code-behind
│   └── MainViewModel.cs   # View model (MVVM)
├── App.xaml              # Application resources
├── App.xaml.cs           # Application startup
└── Program.cs            # Entry point
```

### Design Patterns

- **MVVM**: Model-View-ViewModel pattern for UI separation
- **Dependency Injection**: Microsoft.Extensions.DependencyInjection for service management
- **Reactive**: System.Reactive for event-driven updates
- **Async/Await**: Full async support for non-blocking operations

## Requirements

- .NET 8.0 or higher
- Windows (WPF requirement)
- Soulseek account

## Setup

### 1. Build

```bash
dotnet build
```

### 2. Run

```bash
dotnet run
```

### 3. Configuration

The app stores configuration in:
- `%AppData%\SLSKDONET\config.ini`

First run will create a default configuration. Edit the file to add your Soulseek credentials:

```ini
[Soulseek]
Username = your-username
Password = your-password
ListenPort = 49998
UseUPnP = false
ConnectTimeout = 20000
SearchTimeout = 6000

[Download]
Directory = C:\Users\YourName\Downloads
MaxConcurrent = 2
NameFormat = {artist} - {title}
```

## Usage

### Basic Workflow

1. **Login**: Enter your Soulseek credentials and click "Login"
2. **Search**: Enter a search query and click "Search"
3. **Select**: Click on results to add them to your download queue
4. **Download**: Click "Start Downloads" to begin

### Advanced Options

- **Max Concurrent Downloads**: Limit simultaneous transfers (default: 2)
- **Name Format**: Customize file naming (supports {artist}, {title}, {album})
- **Preferred Formats**: Choose preferred audio formats (mp3, flac, etc.)
- **Bitrate Filtering**: Set minimum/maximum bitrate preferences

## Development

### Adding Features

1. **New Models**: Add to `Models/`
2. **New Services**: Add to `Services/`
3. **New UI Views**: Add to `Views/`
4. **Configuration Options**: Extend `AppConfig.cs` and `ConfigManager.cs`

### Testing

```bash
# Build
dotnet build

# Run tests (when added)
dotnet test
```

## Dependencies

- **Soulseek**: NuGet package for Soulseek.NET protocol
- **CsvHelper**: CSV file parsing
- **TagLibSharp**: Audio metadata reading
- **Microsoft.Extensions.*** : Logging, DI, Configuration
- **System.Reactive**: Reactive extensions

## Related Projects

- **Python Version**: See `Python/` folder for aioslsk-based implementation
- **Reference**: https://github.com/fiso64/slsk-batchdl (C# inspiration)

## Troubleshooting

### Connection Issues

- Verify firewall allows the listening port
- Check Soulseek server status
- Increase `ConnectTimeout` if network is slow

### Search Returns No Results

- Try simpler search queries
- Add artist name explicitly
- Check if content exists on the network

### Downloads Failing

- Verify peer is still online
- Check disk space
- Increase `SearchTimeout` for larger files

## License

GPL-3.0

## Contributing

Contributions welcome! Please follow the existing code style and patterns.
