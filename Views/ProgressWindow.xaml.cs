using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Compresion_RAR.Views
{
    using System.Windows.Threading;

    public partial class ProgressWindow : Window
    {
        private readonly CancellationTokenSource _cts;
        private readonly TaskCompletionSource<bool> _pauseTcs;
        private int _dotCount = 0; 
        private readonly DispatcherTimer _timer;

        public CancellationToken Token => _cts.Token;
        public bool isDecompress = false;

        public ProgressWindow(bool isDecompress)
        {
            InitializeComponent();
            this.isDecompress = isDecompress;
            _cts = new CancellationTokenSource();
            _pauseTcs = new TaskCompletionSource<bool>();
            _pauseTcs.SetResult(false);

            if (isDecompress)
            {
                _timer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(500)
                };
                _timer.Tick += OnTimerTick;
            }
        }

       
        private void OnTimerTick(object sender, EventArgs e)
        {
            _dotCount = (_dotCount + 1) % 4;
            string dots = new string('.', _dotCount);
            StatusText.Text = $"Decompressing{dots}";
        }

        public void StartAnimation()
        {
            _dotCount = 1;
            _timer.Start();
        }

        public void ReportProgress(int percent, string status)
        {
            Dispatcher.Invoke(() =>
            {
                ProgressBar.Value = percent;
                StatusText.Text = status;
            });
        }
        public void StopAnimation()
        {
            _timer.Stop();
        }

     
        public Task WaitIfPausedAsync()
        {
            return _pauseTcs.Task;
        }

        private void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            if (PauseButton.Content.ToString() == "Pause")
            {
                PauseButton.Content = "Resume";
                if (_pauseTcs.Task.IsCompleted)
                {
                    typeof(ProgressWindow)
                        .GetField("_pauseTcs", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                        .SetValue(this, new TaskCompletionSource<bool>());
                }
                if (isDecompress)
                {
                    StopAnimation();
                }
                StatusText.Text = "Paused";
            }
            else
            {
                PauseButton.Content = "Pause";
                _pauseTcs.SetResult(true);
                if (isDecompress)
                {
                    StartAnimation();
                }
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _cts.Cancel();
            StatusText.Text = "Canceled";
            if (isDecompress)
            {
                StopAnimation();
            }
            Close();
        }
    }
}
