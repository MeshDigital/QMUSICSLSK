# Development Guide - SLSKDONET

## Project Setup

### Prerequisites
- .NET 8.0 SDK or later
- Visual Studio 2022 or VS Code with C# extension
- Git

### Getting Started

1. **Clone and Navigate**
   ```bash
   cd SLSKDONET
   ```

2. **Restore Dependencies**
   ```bash
   dotnet restore
   ```

3. **Build**
   ```bash
   dotnet build
   ```

4. **Run**
   ```bash
   dotnet run
   ```

## Code Structure

### Models (`Models/`)
- **Track.cs**: Represents a searchable music track
- **DownloadJob.cs**: Represents a queued download with state

### Services (`Services/`)
- **SoulseekAdapter.cs**: Wrapper around Soulseek.NET client
  - Handles connection, authentication, search, and download
  - Exposes events via reactive EventBus
  
- **DownloadManager.cs**: Orchestrates download jobs
  - Manages concurrent downloads
  - Tracks job state and progress
  - Emits events for UI updates

### Configuration (`Configuration/`)
- **AppConfig.cs**: Configuration model with all settings
- **ConfigManager.cs**: Loads/saves configuration from INI files

### Views (`Views/`)
- **MainWindow.xaml**: Main UI layout
- **MainWindow.xaml.cs**: Code-behind (minimal)
- **MainViewModel.cs**: MVVM view model
  - Binds UI to services
  - Handles user interactions

### Utilities (`Utils/`)
- **FileFormattingUtils.cs**: File formatting and display helpers
- **ValidationUtils.cs**: Input validation methods

## Key Design Decisions

### MVVM Pattern
- Clean separation between UI and business logic
- View models implement `INotifyPropertyChanged` for binding
- Observable collections for list updates

### Dependency Injection
- Services configured in `App.xaml.cs`
- All major components injected as singletons or transients
- Loose coupling between components

### Reactive Patterns
- `System.Reactive` for event-driven updates
- `EventBus` in `SoulseekAdapter` for all events
- `INotifyPropertyChanged` for UI updates

### Async/Await
- All long-running operations are async
- CancellationToken support throughout
- No blocking calls in UI thread

## Common Tasks

### Adding a New Feature

1. **Create Model** if needed in `Models/`
2. **Implement Service Logic** in `Services/`
3. **Add ViewModel Properties** in `MainViewModel.cs`
4. **Update XAML UI** in `MainWindow.xaml`
5. **Wire up Event Handlers**

### Example: Adding Album Support

1. Create `Album.cs` model in `Models/`
2. Add search method to `SoulseekAdapter`
3. Add `ObservableCollection<Album>` to `MainViewModel`
4. Add Album search tab to UI
5. Connect button handlers

### Debugging

**Enable Debug Logging**:
```csharp
// In App.xaml.cs ConfigureServices:
config.SetMinimumLevel(LogLevel.Debug);
```

**Inspect View Model State**:
- Set breakpoints in `MainViewModel` methods
- Check `ObservableCollection` contents in Watch window
- Verify event emissions in Output window

### Testing

Create `Tests/` folder:
```
SLSKDONET.Tests/
├── Services/
│   └── SoulseekAdapterTests.cs
├── Utils/
│   └── ValidationUtilsTests.cs
└── SLSKDONET.Tests.csproj
```

Add to main `.sln` and reference `SLSKDONET` project.

## Performance Considerations

### Concurrent Downloads
- Default: 2 concurrent downloads (configurable)
- Semaphore controls concurrency in `DownloadManager`
- Adjustable via `MaxConcurrentDownloads` config

### Search Results
- Limited to 100 results per search
- Configurable in `SoulseekAdapter.SearchAsync()`
- Consider pagination for large result sets

### UI Updates
- Use `ObservableCollection` for automatic binding
- Collections updated on UI thread (WPF requirement)
- Jobs updated via `JobUpdated` event

## Troubleshooting

### NuGet Package Issues
```bash
dotnet nuget locals all --clear
dotnet restore
```

### Build Errors
```bash
dotnet clean
dotnet build
```

### WPF XAML Intellisense Issues
- Restart Visual Studio
- Clean bin/obj folders
- Rebuild solution

## Resources

- [Soulseek.NET GitHub](https://github.com/jpdillingham/Soulseek.NET)
- [WPF MVVM Pattern](https://docs.microsoft.com/en-us/windows/uwp/get-started/building-a-basic-universal-windows-app-with-xaml-and-c-sharp)
- [System.Reactive Guide](https://github.com/ReactiveX/RxJS/wiki)
- [.NET Async Best Practices](https://docs.microsoft.com/en-us/archive/msdn-magazine/2013/march/async-await-best-practices-in-asynchronous-programming)

## Next Steps

- [ ] Implement batch CSV file support
- [ ] Add configuration profiles
- [ ] Create interactive album download mode
- [ ] Add playlist export (.m3u)
- [ ] Implement user preference persistence
- [ ] Add unit tests
- [ ] Create installer (NSIS or WiX)
