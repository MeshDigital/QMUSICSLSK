using System.Windows;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SLSKDONET.Configuration;
using SLSKDONET.Services;
using SLSKDONET.Services.InputParsers;
using SpotifyAPI.Web;
using SLSKDONET.Views;

namespace SLSKDONET;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    public IServiceProvider Services { get; }

    public App()
    {
        Services = ConfigureServices();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        // Register global exception handlers to surface runtime errors during startup.
        AppDomain.CurrentDomain.UnhandledException += (s, ev) =>
        {
            try
            {
                var ex = ev.ExceptionObject as Exception;
                System.Windows.MessageBox.Show($"Unhandled exception: {ex?.Message}\n\n{ex?.StackTrace}", "Unhandled Exception", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            catch { }
        };

        TaskScheduler.UnobservedTaskException += (s, ev) =>
        {
            try
            {
                System.Windows.MessageBox.Show($"Unobserved task exception: {ev.Exception.Message}\n\n{ev.Exception.StackTrace}", "Unhandled Task Exception", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                ev.SetObserved();
            }
            catch { }
        };

        this.DispatcherUnhandledException += (s, ev) =>
        {
            try
            {
                System.Windows.MessageBox.Show($"Dispatcher exception: {ev.Exception.Message}\n\n{ev.Exception.StackTrace}", "Dispatcher Exception", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                ev.Handled = true;
            }
            catch { }
        };

        base.OnStartup(e);

        try
        {
            var mainWindow = Services.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Startup failed: {ex.Message}\n\n{ex.StackTrace}", "Startup Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            throw;
        }
    }

    /// <summary>
    /// Gets the current <see cref="App"/> instance in use
    /// </summary>
    public new static App Current => (App)System.Windows.Application.Current;
    
    private static IServiceProvider ConfigureServices()
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
        // Register ConfigManager as a singleton service.
        services.AddSingleton<ConfigManager>();
        // Register AppConfig using a factory that resolves ConfigManager from the container.
        // This ensures the configuration is loaded and processed correctly within the DI ecosystem.
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
        services.AddSingleton<DownloadManager>();
        services.AddSingleton<FileNameFormatter>();
        services.AddSingleton<ProtectedDataService>();

        // Register Spotify services
        services.AddSingleton(provider =>
        {
            var config = provider.GetRequiredService<AppConfig>();
            if (string.IsNullOrEmpty(config.SpotifyClientId) || string.IsNullOrEmpty(config.SpotifyClientSecret))
            {
                // Return a null client if not configured. The app can handle this gracefully.
                return null!;
            }
            var spotifyConfig = SpotifyClientConfig.CreateDefault()
                .WithAuthenticator(new ClientCredentialsAuthenticator(config.SpotifyClientId, config.SpotifyClientSecret));
            
            return new SpotifyClient(spotifyConfig);
        });
        services.AddSingleton<SpotifyInputSource>();

        // Input parsers
        services.AddSingleton<CsvInputSource>();
        
        // Download logging
        services.AddSingleton<DownloadLogService>();

        // Register navigation and UI services used by the app.
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<SLSKDONET.Views.INotificationService, NotificationServiceAdapter>();
        services.AddSingleton<IUserInputService, UserInputService>();

        // Views (add logger for MainWindow)
        services.AddSingleton<MainWindow>();
        services.AddSingleton<MainViewModel>(); // MainViewModel can be a singleton as it represents the app's main state.

        // Pages for navigation
        services.AddTransient<SearchPage>();
        services.AddTransient<ImportedPage>();
        services.AddTransient<DownloadsPage>();
        services.AddTransient<LibraryPage>();
        services.AddTransient<SettingsPage>();

        // Utilities can be registered as singletons as they are stateless.
        services.AddSingleton<SearchQueryNormalizer>();
    }
}
