using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using Microsoft.Win32;
using Microsoft.VisualBasic;
using System.Windows.Forms; // For FolderBrowserDialog
using Wpf.Ui.Controls;
using System.Windows.Navigation;
using System;
using SLSKDONET.Services;
using Microsoft.Extensions.Logging;
using System.IO;

namespace SLSKDONET.Views;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly ILogger<MainWindow> _logger;
    private static readonly string _logPath = Path.Combine(Path.GetTempPath(), "SLSKDONET_UI.log");

    public MainWindow(MainViewModel viewModel, INavigationService navigationService, ILogger<MainWindow> logger)
    {
        _logger = logger;
        LogToFile("=== MainWindow Constructor Started ===");
        
        try
        {
            LogToFile("Calling InitializeComponent...");
            InitializeComponent();
            LogToFile("InitializeComponent completed");
            
            _viewModel = viewModel;
            LogToFile("ViewModel assigned");
            
            DataContext = viewModel;
            LogToFile($"DataContext set. IsConnected={viewModel.IsConnected}, IsLoginOverlayVisible={viewModel.IsLoginOverlayVisible}");
            
            // Find controls from XAML
            var passwordBox = this.FindName("OverlayPasswordBox") as System.Windows.Controls.PasswordBox;
            var rootFrame = this.FindName("RootFrame") as Frame;
            
            LogToFile($"Controls found - PasswordBox: {passwordBox != null}, Frame: {rootFrame != null}");
            
            // Wire up PasswordBox to notify LoginCommand when password changes
            if (passwordBox != null)
            {
                passwordBox.PasswordChanged += (s, e) =>
                {
                    // Force re-evaluation of LoginCommand.CanExecute
                    CommandManager.InvalidateRequerySuggested();
                };
                LogToFile("PasswordBox event handler attached");
            }
            
            // Initialize navigation (this will navigate to Search page)
            LogToFile("Initializing navigation...");
            InitializeNavigation(navigationService, rootFrame);
            LogToFile("Navigation initialized");
            
            // Ensure initial library load (async, won't block UI)
            LogToFile("Starting library load...");
            _viewModel.OnViewLoaded();
            LogToFile("Library load initiated");
            
            LogToFile("=== MainWindow Constructor Completed Successfully ===");
        }
        catch (Exception ex)
        {
            var msg = $"MainWindow initialization failed: {ex.Message}\n\n{ex.StackTrace}";
            LogToFile($"ERROR: {msg}");
            System.Windows.MessageBox.Show(msg, 
                "Initialization Error", 
                System.Windows.MessageBoxButton.OK, 
                System.Windows.MessageBoxImage.Error);
            throw;
        }
    }
    
    private void LogToFile(string message)
    {
        try
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            File.AppendAllText(_logPath, $"[{timestamp}] {message}\n");
            _logger?.LogInformation(message);
        }
        catch { }
    }

    private void InitializeNavigation(INavigationService navigationService, Frame? rootFrame)
    {
        try
        {
            LogToFile("Registering navigation pages...");
            // Register all pages before navigating so the frame can resolve them.
            navigationService.RegisterPage("Search", typeof(SearchPage));
            navigationService.RegisterPage("Library", typeof(LibraryPage));
            navigationService.RegisterPage("Downloads", typeof(DownloadsPage));
            navigationService.RegisterPage("Settings", typeof(SettingsPage));
                        navigationService.RegisterPage("ImportPreview", typeof(ImportPreviewPage));
            LogToFile("Pages registered");

            if (rootFrame != null)
            {
                LogToFile("Setting frame...");
                navigationService.SetFrame(rootFrame);
                LogToFile("Frame set");
                
                LogToFile("Navigating to Search page...");
                navigationService.NavigateTo("Search"); // Set the startup page
                LogToFile("Navigation to Search completed");
            }
            else
            {
                LogToFile("WARNING: RootFrame is null, skipping navigation setup");
            }
        }
        catch (Exception ex)
        {
            LogToFile($"InitializeNavigation ERROR: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
    }

    private void SignInButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            LogToFile("=== Sign In Button Clicked ===");
            
            var passwordBox = this.FindName("OverlayPasswordBox") as System.Windows.Controls.PasswordBox;
            if (passwordBox == null)
            {
                LogToFile("ERROR: PasswordBox not found");
                System.Windows.MessageBox.Show("Login form error - password box not found", "Error");
                return;
            }

            var password = passwordBox.Password;
            LogToFile($"Password length: {password?.Length ?? 0}");
            LogToFile($"Username: {_viewModel.Username}");
            LogToFile($"IsConnected: {_viewModel.IsConnected}");

            if (string.IsNullOrEmpty(_viewModel.Username))
            {
                _viewModel.StatusText = "Please enter a username";
                LogToFile("Login cancelled - no username");
                return;
            }

            if (string.IsNullOrEmpty(password))
            {
                _viewModel.StatusText = "Please enter a password";
                LogToFile("Login cancelled - no password");
                return;
            }

            // Execute the LoginCommand with the password
            if (_viewModel.LoginCommand.CanExecute(password))
            {
                LogToFile("Executing LoginCommand...");
                _viewModel.LoginCommand.Execute(password);
                LogToFile("LoginCommand executed");
            }
            else
            {
                LogToFile("LoginCommand.CanExecute returned false");
                _viewModel.StatusText = "Cannot login at this time";
            }
        }
        catch (Exception ex)
        {
            LogToFile($"SignInButton_Click ERROR: {ex.Message}\n{ex.StackTrace}");
            System.Windows.MessageBox.Show($"Login error: {ex.Message}", "Error");
        }
    }

}
