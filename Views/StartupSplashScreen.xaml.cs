using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace SLSKDONET.Views
{
    public partial class StartupSplashScreen : Window
    {
        private readonly List<string> _loadingMessages = new()
        {
            "Reticulating splines...",
            "Initializing flux capacitor...",
            "Downloading more RAM...",
            "Compiling coffee into code...",
            "Teaching robots to love...",
            "Dividing by zero... just kidding!",
            "Convincing electrons to flow...",
            "Calibrating quantum harmonics...",
            "Reversing entropy...",
            "Optimizing bit flipping algorithms...",
            "Warming up the hamster wheel...",
            "Consulting the magic 8-ball...",
            "Asking ChatGPT for help...",
            "Spinning up the blockchain...",
            "Deploying AI overlords...",
            "Initializing Soulseek adapter...",
            "Loading your questionable music taste...",
            "Preparing to judge your playlists...",
            "Connecting to the matrix...",
            "Defragmenting the cloud...",
            "Untangling headphone cables...",
            "Tuning the warp drive...",
            "Feeding the database hamsters...",
            "Polishing the UI pixels...",
            "Summoning the download demons...",
            "Activating turbo mode...",
            "Bypassing the firewall... legally!",
            "Initializing the thing-a-ma-jig...",
            "Loading cat videos... wait, wrong app!",
            "Preparing your sonic experience..."
        };

        private readonly DispatcherTimer _progressTimer;
        private readonly DispatcherTimer _messageTimer;
        private readonly Random _random = new();
        private double _currentProgress = 0;
        private bool _canClose = false;

        public StartupSplashScreen()
        {
            InitializeComponent();
            
            // Center on screen
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            
            // Start with a random message
            UpdateMessage();

            // Fake progress animation - fills to 90% over 2 seconds
            _progressTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            _progressTimer.Tick += (s, e) =>
            {
                if (_currentProgress < 90)
                {
                    _currentProgress += 2.25; // 90% in 2 seconds (40 ticks * 2.25 = 90)
                    ProgressBar.Value = _currentProgress;
                    PercentageText.Text = $"{_currentProgress:F0}%";
                }
                else
                {
                    _progressTimer.Stop();
                }
            };
            _progressTimer.Start();

            // Change message every 800ms
            _messageTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(800)
            };
            _messageTimer.Tick += (s, e) => UpdateMessage();
            _messageTimer.Start();
        }

        private void UpdateMessage()
        {
            var message = _loadingMessages[_random.Next(_loadingMessages.Count)];
            LoadingText.Text = message;

            // Pulse animation for the loading text
            var animation = new DoubleAnimation
            {
                From = 0.6,
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(400),
                AutoReverse = true
            };
            LoadingText.BeginAnimation(OpacityProperty, animation);
        }

        /// <summary>
        /// Call this when the main app is ready to show.
        /// This will complete the progress bar and close the splash.
        /// </summary>
        public async Task NotifyLoadingComplete()
        {
            _canClose = true;
            
            await Dispatcher.InvokeAsync(async () =>
            {
                // Stop timers
                _progressTimer?.Stop();
                _messageTimer?.Stop();

                // Complete the progress bar
                LoadingText.Text = "Ready!";
                
                var completeAnimation = new DoubleAnimation
                {
                    To = 100,
                    Duration = TimeSpan.FromMilliseconds(200)
                };
                ProgressBar.BeginAnimation(System.Windows.Controls.Primitives.RangeBase.ValueProperty, completeAnimation);
                PercentageText.Text = "100%";

                // Brief pause to show completion
                await Task.Delay(300);

                // Fade out
                var fadeOut = new DoubleAnimation
                {
                    From = 1.0,
                    To = 0.0,
                    Duration = TimeSpan.FromMilliseconds(300)
                };

                fadeOut.Completed += (s, e) => Close();
                BeginAnimation(OpacityProperty, fadeOut);
            });
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // Prevent closing until NotifyLoadingComplete is called
            if (!_canClose)
            {
                e.Cancel = true;
            }
            base.OnClosing(e);
        }
    }
}
