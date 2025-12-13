<<<<<<< Updated upstream
=======
// App.axaml.cs (Avalonia Application & DI Setup)

>>>>>>> Stashed changes
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SLSKDONET.Configuration;
<<<<<<< Updated upstream
using SLSKDONET.Services;
using SLSKDONET.Services.InputParsers;
using SLSKDONET.ViewModels;
using System;
using System.IO;

namespace SLSKDONET;

/// <summary>
/// Avalonia application class for cross-platform UI
/// </summary>
public partial class AvaloniaApp : Application
{
    public IServiceProvider? Services { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Configure services
            Services = ConfigureServices();

            try
            {
                // Initialize database before anything else
                var databaseService = Services.GetRequiredService<DatabaseService>();
                databaseService.InitAsync().GetAwaiter().GetResult();

                // Create main window
                var mainWindow = new Views.Avalonia.MainWindow
                {
                    DataContext = Services.GetRequiredService<MainViewModel>()
                };

                // Eagerly instantiate LibraryViewModel so it subscribes to ProjectAdded events
                var libraryViewModel = Services.GetRequiredService<LibraryViewModel>();
                var mainViewModel = Services.GetRequiredService<MainViewModel>();
                libraryViewModel.SetMainViewModel(mainViewModel);

                // Start the Download Manager loop
                var downloadManager = Services.GetRequiredService<DownloadManager>();
                downloadManager.InitAsync().GetAwaiter().GetResult();
                _ = downloadManager.StartAsync();

                desktop.MainWindow = mainWindow;
            }
            catch (Exception ex)
            {
                // Log startup error
                Console.WriteLine($"Startup failed: {ex.Message}\n{ex.StackTrace}");
                throw;
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();
        ConfigureSharedServices(services);
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Shared service configuration used by both WPF and Avalonia
    /// </summary>
    public static void ConfigureSharedServices(IServiceCollection services)
    {
        // Logging
        services.AddLogging(config =>
        {
            config.ClearProviders();
            config.AddConsole();
            config.SetMinimumLevel(LogLevel.Information);
        });

        // Configuration
        services.AddSingleton<ConfigManager>();
        services.AddSingleton(provider =>
        {
            var configManager = provider.GetRequiredService<ConfigManager>();
            var appConfig = configManager.Load();
            if (string.IsNullOrEmpty(appConfig.DownloadDirectory))
                appConfig.DownloadDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "SLSKDONET");
            return appConfig;
        });

        // Services
        services.AddSingleton<SoulseekAdapter>();
        services.AddSingleton<FileNameFormatter>();
        services.AddSingleton<ProtectedDataService>();

        // Spotify services
        services.AddSingleton<SpotifyInputSource>();
        services.AddSingleton<SpotifyScraperInputSource>();

        // Input parsers
        services.AddSingleton<CsvInputSource>();

        // Import Plugin System
        services.AddSingleton<ImportOrchestrator>();
        services.AddSingleton<IImportProvider, Services.ImportProviders.SpotifyImportProvider>();
        services.AddSingleton<IImportProvider, Services.ImportProviders.CsvImportProvider>();

        // Library Action System
        services.AddSingleton<Services.LibraryActions.LibraryActionProvider>();
        services.AddSingleton<Services.LibraryActions.ILibraryAction, Services.LibraryActions.OpenFolderAction>();
        services.AddSingleton<Services.LibraryActions.ILibraryAction, Services.LibraryActions.RemoveFromPlaylistAction>();
        services.AddSingleton<Services.LibraryActions.ILibraryAction, Services.LibraryActions.DeletePlaylistAction>();

        // Download logging and library management
        services.AddSingleton<DownloadLogService>();
        services.AddSingleton<LibraryService>();
        services.AddSingleton<ILibraryService>(provider => provider.GetRequiredService<LibraryService>());

        // Audio Player
        services.AddSingleton<IAudioPlayerService, AudioPlayerService>();
        services.AddSingleton<PlayerViewModel>();

        // Metadata and tagging service
        services.AddSingleton<ITaggerService, MetadataTaggerService>();

        // Rekordbox export service
        services.AddSingleton<RekordboxXmlExporter>();

        // Download manager
        services.AddSingleton<DownloadManager>();

        // Database
        services.AddSingleton<DatabaseService>();
        services.AddSingleton<IMetadataService, MetadataService>();

        // ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<LibraryViewModel>();
        services.AddSingleton<ImportPreviewViewModel>();
        services.AddSingleton<ImportHistoryViewModel>();

        // Utilities
        services.AddSingleton<SearchQueryNormalizer>();
=======
using SLSKDONET.Services.InputParsers;
using SLSKDONET.Services;
using SLSKDONET.ViewModels;
using SLSKDONET.Views;
using System;
using System.IO;
using System.Linq;

namespace SLSKDONET
{
    public partial class App : Application
    {
        public IServiceProvider Services { get; private set; }

        public override void Initialize()
        {
            Services = ConfigureServices();
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // In Avalonia, the MainViewModel/Shell Window is resolved here.
                // Note: The original logic for database init and download manager start 
                // should be moved/re-evaluated based on Avalonia's lifecycle.
                
                // For now, only assign the DataContext to the main window.
                // We'll address the window itself later.
                // desktop.MainWindow = Services.GetRequiredService<MainWindow>(); // Placeholder for MainWindow

                // We'll initialize the main window/shell in a subsequent step.
            }

            base.OnFrameworkInitializationCompleted();
        }

        // Adapted from original WPF App.xaml.cs
        private IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            return services.BuildServiceProvider();
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            // Logging
            services.AddLogging(config =>
            {
                config.ClearProviders();
                config.AddConsole();
                config.SetMinimumLevel(LogLevel.Information);
            });

            // Configuration
            services.AddSingleton<ConfigManager>();
            services.AddSingleton(provider =>
            {
                var configManager = provider.GetRequiredService<ConfigManager>();
                var appConfig = configManager.Load();
                if (string.IsNullOrEmpty(appConfig.DownloadDirectory))
                    appConfig.DownloadDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "SLSKDONET");
                return appConfig;
            });

            // Services
            services.AddSingleton<SoulseekAdapter>();
            services.AddSingleton<FileNameFormatter>();
            services.AddSingleton<ProtectedDataService>();
            
            // Audio Player
            services.AddSingleton<IAudioPlayerService, AudioPlayerService>();
            services.AddSingleton<PlayerViewModel>();
            
            // Core ViewModels
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<DatabaseService>();
            services.AddSingleton<IMetadataService, MetadataService>();
            services.AddSingleton<ILibraryService, LibraryService>();
            services.AddSingleton<DownloadManager>();
            services.AddSingleton<ITaggerService, TaggerService>();
            services.AddSingleton<IUserInputService, UserInputService>();
            services.AddSingleton<INavigationService, NavigationService>();
            
            // Pages for navigation
            // Note: Views are typically registered as UserControl or Window
            services.AddSingleton<SLSKDONET.ViewModels.LibraryViewModel>();
            services.AddSingleton<ImportPreviewViewModel>();
            services.AddSingleton<ImportHistoryViewModel>();
            services.AddSingleton<SpotifyImportViewModel>();
            services.AddTransient<SearchPage>();
            services.AddTransient<DownloadsPage>();
            services.AddTransient<LibraryPage>();
            services.AddTransient<SettingsPage>();
            // services.AddTransient<MainWindow>(); // Placeholder for MainWindow

            // Utilities
            services.AddSingleton<SearchQueryNormalizer>();
        }
>>>>>>> Stashed changes
    }
}
