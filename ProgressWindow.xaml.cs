using System.Windows;

namespace NowPlayingPopup
{
    public partial class ProgressWindow : Window
    {
        public ProgressWindow()
        {
            InitializeComponent();
        }

        /// Cập nhật giá trị progress (0 - 100).
        public void SetProgress(int percent)
        {
            if (percent < 0) percent = 0;
            if (percent > 100) percent = 100;

            Dispatcher.Invoke(() =>
            {
                ProgressBar.Value = percent;
            });
        }
    }
}
