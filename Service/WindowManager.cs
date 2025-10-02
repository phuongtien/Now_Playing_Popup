using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NowPlayingPopup.Models;

namespace NowPlayingPopup.Services
{
    /// <summary>
    /// Manages window positioning, sizing, and styling
    /// </summary>
    public class WindowManager
    {
        private readonly Window _window;

        public WindowManager(Window window)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
        }

        public void ApplySettings(WidgetSettings settings)
        {
            try
            {
                _window.Dispatcher.Invoke(() =>
                {
                    var (width, height) = settings.GetDimensions();
                    _window.Width = width;
                    _window.Height = height;

                    if (settings.RememberPosition)
                    {
                        _window.Left = settings.PositionX;
                        _window.Top = settings.PositionY;
                        EnsureWindowOnScreen();
                    }
                    else
                    {
                        ApplyPopupPosition(settings.PopupPosition);
                    }

                    RemoveWPFBorderStyling();
                    ApplyWindowProperties(settings);
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ApplySettings error: {ex}");
            }
        }

        public void ApplyPopupPosition(string position)
        {
            var screen = SystemParameters.WorkArea;
            switch (position)
            {
                case "top-left":
                    _window.Left = screen.Left;
                    _window.Top = screen.Top;
                    break;
                case "top-right":
                    _window.Left = screen.Right - _window.Width;
                    _window.Top = screen.Top;
                    break;
                case "bottom-left":
                    _window.Left = screen.Left;
                    _window.Top = screen.Bottom - _window.Height;
                    break;
                case "bottom-right":
                default:
                    _window.Left = screen.Right - _window.Width;
                    _window.Top = screen.Bottom - _window.Height;
                    break;
            }
        }

        private void RemoveWPFBorderStyling()
        {
            var border = _window.FindName("MainBorder") as Border;
            if (border == null) return;

            border.Background = new SolidColorBrush(Colors.Transparent);
            border.BorderThickness = new Thickness(0);
            border.BorderBrush = null;
            border.CornerRadius = new CornerRadius(0);
            border.Effect = null;
            border.Padding = new Thickness(0);
            border.Margin = new Thickness(0);
        }

        private void EnsureWindowOnScreen()
        {
            var workingArea = SystemParameters.WorkArea;
            const double tolerance = 10;

            if (_window.Left < workingArea.Left - tolerance)
                _window.Left = workingArea.Left;
            if (_window.Top < workingArea.Top - tolerance)
                _window.Top = workingArea.Top;
            if (_window.Left + _window.Width > workingArea.Right + tolerance)
                _window.Left = workingArea.Right - _window.Width;
            if (_window.Top + _window.Height > workingArea.Bottom + tolerance)
                _window.Top = workingArea.Bottom - _window.Height;
        }

        private void ApplyWindowProperties(WidgetSettings settings)
        {
            _window.Opacity = Math.Max(0.1, Math.Min(1.0, settings.Opacity));
            _window.Topmost = settings.AlwaysOnTop;
        }

        public void MinimizeToTray()
        {
            _window.Hide();
            _window.ShowInTaskbar = false;
        }

        public void RestoreFromTray()
        {
            _window.Show();
            _window.WindowState = WindowState.Normal;
            _window.Activate();
        }
    }
}