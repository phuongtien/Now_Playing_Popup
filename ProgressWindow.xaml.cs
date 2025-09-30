using System.Windows;

namespace NowPlayingPopup
{
    public partial class ProgressWindow : Window
    {
        public ProgressWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Cập nhật giá trị progress (0 - 100).
        /// </summary>
        public void SetProgress(int percent)
        {
            if (percent < 0) percent = 0;
            if (percent > 100) percent = 100;

            Dispatcher.Invoke(() =>
            {
                ProgressBar.Value = percent;
                // PercentText sẽ tự động update thông qua Binding
            });
        }
    }
}