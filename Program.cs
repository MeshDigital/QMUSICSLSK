<<<<<<< Updated upstream
using System;

namespace SLSKDONET;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
#if NET8_0_WINDOWS
        // WPF entry point for Windows
        var app = new App();
        app.InitializeComponent();
        app.Run();
#else
        // Avalonia entry point for cross-platform
        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
#endif
    }

#if !NET8_0_WINDOWS
    // Avalonia configuration, don't remove; also used by visual designer.
    public static Avalonia.AppBuilder BuildAvaloniaApp()
        => Avalonia.AppBuilder.Configure<AvaloniaApp>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
#endif
=======
// Program.cs (Standard Avalonia Desktop Entry Point)

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.ReactiveUI;
using System;

namespace SLSKDONET
{
    class Program
    {
        // Initialization code. Don't use any Avalonia or WPF types until BuildAvaloniaApp
        // is called.
        [STAThread]
        public static void Main(string[] args) => BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .LogToTrace()
                .UseReactiveUI();
    }
>>>>>>> Stashed changes
}
