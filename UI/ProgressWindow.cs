using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using WpfProgressBar = System.Windows.Controls.ProgressBar;

namespace NowPlayingPopup.UI
{
    /// <summary>
    /// Progress window for displaying download/operation progress
    /// </summary>
    public partial class ProgressWindow : Window
    {
        private readonly WpfProgressBar _progressBar;
        private readonly TextBlock _statusText;
        private readonly DispatcherTimer _animationTimer;
        private int _animationStep = 0;

        public ProgressWindow()
        {
            InitializeWindowContent();

            // Get references to controls
            _progressBar = (WpfProgressBar)FindName("ProgressBar");
            _statusText = (TextBlock)FindName("StatusText");

            // Setup animation timer for indeterminate progress
            _animationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            _animationTimer.Tick += AnimationTimer_Tick;
        }

        private void InitializeWindowContent()
        {
            Width = 400;
            Height = 120;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = System.Windows.Media.Brushes.White;
            Title = "Progress";

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var progressBar = new WpfProgressBar
            {
                Name = "ProgressBar",
                Height = 20,
                Margin = new Thickness(20),
                Minimum = 0,
                Maximum = 100
            };
            Grid.SetRow(progressBar, 0);
            RegisterName("ProgressBar", progressBar);

            var statusText = new TextBlock
            {
                Name = "StatusText",
                Text = "Please wait...",
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Margin = new Thickness(20, 0, 20, 20),
                FontSize = 14
            };
            Grid.SetRow(statusText, 1);
            RegisterName("StatusText", statusText);

            grid.Children.Add(progressBar);
            grid.Children.Add(statusText);

            Content = grid;
        }

        /// <summary>
        /// Sets determinate progress (0-100)
        /// </summary>
        public void SetProgress(int percent)
        {
            if (percent < 0) percent = 0;
            if (percent > 100) percent = 100;

            Dispatcher.Invoke(() =>
            {
                StopIndeterminateMode();
                _progressBar.IsIndeterminate = false;
                _progressBar.Value = percent;

                if (_statusText != null)
                {
                    _statusText.Text = $"{percent}%";
                }
            });
        }

        /// <summary>
        /// Sets progress with custom message
        /// </summary>
        public void SetProgress(int percent, string message)
        {
            SetProgress(percent);
            SetStatus(message);
        }

        /// <summary>
        /// Sets status message
        /// </summary>
        public void SetStatus(string message)
        {
            Dispatcher.Invoke(() =>
            {
                if (_statusText != null)
                {
                    _statusText.Text = message;
                }
            });
        }

        /// <summary>
        /// Shows indeterminate progress (spinning/animated)
        /// </summary>
        public void ShowIndeterminate()
        {
            Dispatcher.Invoke(() =>
            {
                _progressBar.IsIndeterminate = true;
                _animationTimer.Start();
            });
        }

        /// <summary>
        /// Stops indeterminate mode
        /// </summary>
        public void StopIndeterminateMode()
        {
            _animationTimer.Stop();
            _animationStep = 0;
        }

        private void AnimationTimer_Tick(object? sender, EventArgs e)
        {
            _animationStep = (_animationStep + 1) % 100;
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            StopIndeterminateMode();
            _animationTimer.Tick -= AnimationTimer_Tick;
        }
    }
}