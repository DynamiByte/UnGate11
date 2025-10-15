using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Media;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace UnGate11
{
    public partial class MainWindow : Window, IDisposable
    {
        // Constants
        private const int INVALID_CLICK_THRESHOLD = 3;
        private const int BUTTON_CONTENT_DELAY_MS = 2000;
        private const int TASK_COMPLETION_DELAY_MS = 500;
        private const double GIF_FRAME_DELAY_MS = 24;
        

        // Fields
        // GIF animation
        private BitmapDecoder _gifDecoder;
        private int _gifFrameIndex;
        private DispatcherTimer _gifTimer;
        private DispatcherTimer _delayTimer;

        // State management
        private string _currentPatchState = "idkbruh";
        private bool _interactionEnabled = true;
        private int _invalidClickCount = 0;
        private bool _disposed = false;

        // System helper
        private readonly WindowsSystemHelper _systemHelper;

        // UI elements
        private TextBlock _progressTextBlock;

        // Cached storyboards
        private Storyboard _fadeLogoIn, _fadeLogoOut;
        private Storyboard _fadeLoadingIn, _fadeLoadingOut;
        private Storyboard _fadePatchButtonIn, _fadePatchButtonOut;
        private Storyboard _fadeRefreshButtonIn, _fadeRefreshButtonOut;
        

        // Constructor and Initialization
        public MainWindow()
        {
            InitializeComponent();
            InitializeProgressTextBlock();
            CacheStoryboards();

            // Initialize system helper directly in constructor
            _systemHelper = new WindowsSystemHelper();
            _systemHelper.PatchStatusChecked += SystemHelper_PatchStatusChecked;
            _systemHelper.PatchActionCompleted += SystemHelper_PatchActionCompleted;
            _systemHelper.WindowsUpdateRefreshed += SystemHelper_WindowsUpdateRefreshed;
            _systemHelper.ProgressReported += SystemHelper_ProgressReported;

            InitializeVersionLabel();

            StateChanged += MainWindow_StateChanged;
            PatchButton.Content = "Checking Patch State...";

            // Check patch status using helper
            Task.Run(async () => await _systemHelper.CheckPatchStatus());

            // Start loading animation
            StartTask(null);
        }

        private void InitializeProgressTextBlock()
        {
            _progressTextBlock = new TextBlock
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Foreground = Brushes.White,
                FontSize = 14,
                Opacity = 0,
                Visibility = Visibility.Collapsed,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 300,
                Margin = new Thickness(0, -50, 0, 0)
            };

            if (Content is Grid mainGrid)
            {
                mainGrid.Children.Add(_progressTextBlock);
            }
        }

        private void CacheStoryboards()
        {
            _fadeLogoIn = (Storyboard)FindResource("FadeLogoIn");
            _fadeLogoOut = (Storyboard)FindResource("FadeLogoOut");
            _fadeLoadingIn = (Storyboard)FindResource("FadeLoadingIn");
            _fadeLoadingOut = (Storyboard)FindResource("FadeLoadingOut");
            _fadePatchButtonIn = (Storyboard)FindResource("FadePatchButtonIn");
            _fadePatchButtonOut = (Storyboard)FindResource("FadePatchButtonOut");
            _fadeRefreshButtonIn = (Storyboard)FindResource("FadeRefreshButtonIn");
            _fadeRefreshButtonOut = (Storyboard)FindResource("FadeRefreshButtonOut");
        }

        private void InitializeVersionLabel()
        {
            var version = GetVersion();
            VersionLabel.Content = $"v{version.Major}.{version.Minor}.{version.Build}";
        }
        

        // System Helper Event Handlers
        private void SystemHelper_PatchStatusChecked(object sender, WindowsSystemHelper.StatusEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                switch (e.StatusCode)
                {
                    case "C1":
                        PatchButton.Content = "Patch";
                        _currentPatchState = "unpatched";
                        EndTask();
                        break;
                    case "C0":
                        PatchButton.Content = "Unpatch";
                        _currentPatchState = "patched";
                        EndTask();
                        break;
                    default:
                        PatchButton.Content = "Patch";
                        EndTask();
                        break;
                }
            });
        }

        private void SystemHelper_PatchActionCompleted(object sender, WindowsSystemHelper.StatusEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                switch (e.StatusCode)
                {
                    case "P0":
                        _currentPatchState = "patched";
                        PatchButton.Content = "Patch Applied";
                        DelayedContent(PatchButton, "Unpatch", BUTTON_CONTENT_DELAY_MS);
                        EndTask();
                        break;
                    case "P1":
                        _currentPatchState = "unpatched";
                        PatchButton.Content = "Patch Removed";
                        DelayedContent(PatchButton, "Patch", BUTTON_CONTENT_DELAY_MS);
                        EndTask();
                        break;
                    default:
                        EndTask();
                        break;
                }
            });
        }

        private void SystemHelper_WindowsUpdateRefreshed(object sender, WindowsSystemHelper.StatusEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                if (e.StatusCode == "WUR")
                {
                    UpdateRefreshButton.Content = "Refreshed";
                    DelayedContent(UpdateRefreshButton, "Refresh Windows Update", BUTTON_CONTENT_DELAY_MS);
                    EndTask();
                }
            });
        }

        private void SystemHelper_ProgressReported(object sender, WindowsSystemHelper.ProgressEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                _progressTextBlock.Text = e.Message;
            });
        }
        

        // Task Management
        private void SetInteractionEnabled(bool state)
        {
            _interactionEnabled = state;
            PatchButton.IsEnabled = state;
            UpdateRefreshButton.IsEnabled = state;
        }

        private void EndTask()
        {
            if (_delayTimer == null)
            {
                _delayTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(TASK_COMPLETION_DELAY_MS) };
            }

            _delayTimer.Tick += OnEndTaskDelay;
            _delayTimer.Start();
        }

        private void OnEndTaskDelay(object sender, EventArgs e)
        {
            _delayTimer.Stop();
            _delayTimer.Tick -= OnEndTaskDelay;

            _fadeLogoIn.Begin();
            _fadeLoadingOut.Begin();
            _fadePatchButtonIn.Begin();
            _fadeRefreshButtonIn.Begin();

            _progressTextBlock.Visibility = Visibility.Collapsed;
            _progressTextBlock.Opacity = 0;

            StopLoadingGIF();
            SetInteractionEnabled(true);
            _invalidClickCount = 0;
        }

        private void StartTask(string type)
        {
            if (type == "patch")
            {
                PatchButton.Content = _currentPatchState == "unpatched" ? "Patching" : "Unpatching";
                _progressTextBlock.Visibility = Visibility.Visible;
                _progressTextBlock.Opacity = 1;
            }
            else if (type == "refresh")
            {
                UpdateRefreshButton.Content = "Refreshing";
                _progressTextBlock.Visibility = Visibility.Visible;
                _progressTextBlock.Opacity = 1;
                _progressTextBlock.Text = "Starting Windows Update refresh...";
            }
            else
            {
                _progressTextBlock.Visibility = Visibility.Collapsed;
                _progressTextBlock.Opacity = 0;
            }

            SetInteractionEnabled(false);
            _fadeLogoOut.Begin();
            _fadePatchButtonOut.Begin();
            _fadeRefreshButtonOut.Begin();
            _fadeLoadingIn.Begin();
            StartLoadingGIF();
        }

        private async void DelayedContent(ContentControl control, object content, int delayMs)
        {
            await Task.Delay(delayMs).ConfigureAwait(false);
            await Dispatcher.InvokeAsync(() => control.Content = content);
        }
        

        // GIF Animation
        private void LoadGifDecoder()
        {
            if (_gifDecoder != null) return;

            string tempGIFPath = Path.Combine(Path.GetTempPath(), "load.gif");
            if (!File.Exists(tempGIFPath))
                ExtractResourceToFile("load.gif", tempGIFPath);

            using (var fileStream = new FileStream(tempGIFPath, FileMode.Open, FileAccess.Read))
            {
                _gifDecoder = BitmapDecoder.Create(fileStream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            }
        }

        private void StartLoadingGIF()
        {
            LoadGifDecoder();

            _gifFrameIndex = 0;
            LoadingGIF.Source = _gifDecoder.Frames[_gifFrameIndex];
            LoadingGIF.Visibility = Visibility.Visible;
            LoadingGIF.Opacity = 1;

            if (_gifTimer == null)
            {
                _gifTimer = new DispatcherTimer(DispatcherPriority.Render)
                {
                    Interval = TimeSpan.FromMilliseconds(GIF_FRAME_DELAY_MS)
                };
                _gifTimer.Tick += GifTimer_Tick;
            }
            _gifTimer.Start();
        }

        private void GifTimer_Tick(object sender, EventArgs e)
        {
            _gifFrameIndex = (_gifFrameIndex + 1) % _gifDecoder.Frames.Count;
            LoadingGIF.Source = _gifDecoder.Frames[_gifFrameIndex];
        }

        private void StopLoadingGIF()
        {
            _gifTimer?.Stop();
            LoadingGIF.Visibility = Visibility.Collapsed;
            LoadingGIF.Opacity = 0;
        }


        // Event Handlers
        private async void PatchButton_Left(object sender, MouseButtonEventArgs e)
        {
            if (!PatchButton.IsEnabled)
            {
                HandleInvalidButtonClick(PatchButton);
                return;
            }
            StartTask("patch");

            if (_currentPatchState == "unpatched")
            {
                await _systemHelper.ApplyPatch();
            }
            else if (_currentPatchState == "patched")
            {
                await _systemHelper.RemovePatch();
            }
        }

        private void PatchButton_Right(object sender, MouseButtonEventArgs e)
        {
            // Reserved for future functionality
        }

        private async void UpdateRefreshButton_Left(object sender, MouseButtonEventArgs e)
        {
            if (!UpdateRefreshButton.IsEnabled)
            {
                HandleInvalidButtonClick(UpdateRefreshButton);
                return;
            }

            StartTask("refresh");

            await _systemHelper.RefreshWindowsUpdate();
        }

        private void UpdateRefreshButton_Right(object sender, MouseButtonEventArgs e)
        {
            // Reserved for future functionality
        }

        private void CloseWindow(object sender, MouseButtonEventArgs e)
        {
            if (!_interactionEnabled)
            {
                HandleInvalidCloseClick();
                return;
            }

            var storyboard = (Storyboard)FindResource("SlideOutWindow");
            storyboard.Completed += (s, args) =>
            {
                Application.Current.Shutdown();
            };
            storyboard.Begin(this);
        }

        private void HandleInvalidButtonClick(ContentControl button)
        {
            _invalidClickCount++;
            PlayErrorSound();
            if (_invalidClickCount >= INVALID_CLICK_THRESHOLD)
            {
                MessageBox.Show("You can't do that right now. Please wait until the current task is finished.", "Wait", MessageBoxButton.OK, MessageBoxImage.Warning);
                _invalidClickCount = 0;
            }
        }

        private void HandleInvalidCloseClick()
        {
            _invalidClickCount++;
            PlayErrorSound();
            WiggleWindow();
            if (_invalidClickCount >= INVALID_CLICK_THRESHOLD)
            {
                MessageBox.Show("You can't close the window right now. Please wait until current task is finished.\nIf you really want to close it anyway, right-click the close button.", "Wait", MessageBoxButton.OK, MessageBoxImage.Warning);
                _invalidClickCount = 0;
            }
        }

        private void WiggleWindow()
        {
            var wiggle = new DoubleAnimationUsingKeyFrames
            {
                Duration = TimeSpan.FromMilliseconds(400)
            };
            wiggle.KeyFrames.Add(new EasingDoubleKeyFrame(Left, KeyTime.FromPercent(0)));
            wiggle.KeyFrames.Add(new EasingDoubleKeyFrame(Left - 10, KeyTime.FromPercent(0.2)));
            wiggle.KeyFrames.Add(new EasingDoubleKeyFrame(Left + 10, KeyTime.FromPercent(0.4)));
            wiggle.KeyFrames.Add(new EasingDoubleKeyFrame(Left - 7, KeyTime.FromPercent(0.6)));
            wiggle.KeyFrames.Add(new EasingDoubleKeyFrame(Left, KeyTime.FromPercent(1)));

            Storyboard.SetTarget(wiggle, this);
            Storyboard.SetTargetProperty(wiggle, new PropertyPath("Left"));

            var sb = new Storyboard();
            sb.Children.Add(wiggle);
            sb.Begin();
        }

        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Normal)
            {
                var fadeInStoryboard = (Storyboard)FindResource("FadeInWindow");
                fadeInStoryboard.Begin(this);
            }
        }

        private void MinimizeWindow(object sender, MouseButtonEventArgs e)
        {
            var storyboard = (Storyboard)FindResource("FadeOutWindow");
            storyboard.Completed += (s, args) =>
            {
                WindowState = WindowState.Minimized;
                Opacity = 1;
            };
            storyboard.Begin(this);
        }

        private void Info(object sender, MouseButtonEventArgs e) => Process.Start(new ProcessStartInfo { FileName = "https://github.com/DynamiByte/UnGate11/blob/master/README.md" });

        private void DragWindow(object sender, MouseButtonEventArgs e) => DragMove();

        private void PlayErrorSound() => SystemSounds.Hand.Play();

        private Version GetVersion() => Assembly.GetExecutingAssembly().GetName().Version;
        

        // Resource Management
        private void ExtractResourceToFile(string resourceFileName, string destinationPath)
        {
            var assembly = Assembly.GetExecutingAssembly();
            string resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith(resourceFileName, StringComparison.OrdinalIgnoreCase));
            if (resourceName == null) return;

            using (var stream = assembly.GetManifestResourceStream(resourceName))
            using (var file = new FileStream(destinationPath, FileMode.Create, FileAccess.Write))
            {
                stream.CopyTo(file);
            }
        }
        

        // IDisposable Implementation
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Dispose managed resources
                    if (_gifTimer != null)
                    {
                        _gifTimer.Stop();
                        _gifTimer.Tick -= GifTimer_Tick;
                        _gifTimer = null;
                    }

                    if (_delayTimer != null)
                    {
                        _delayTimer.Stop();
                        _delayTimer = null;
                    }

                    _gifDecoder = null;

                    // Unsubscribe from events
                    if (_systemHelper != null)
                    {
                        _systemHelper.PatchStatusChecked -= SystemHelper_PatchStatusChecked;
                        _systemHelper.PatchActionCompleted -= SystemHelper_PatchActionCompleted;
                        _systemHelper.WindowsUpdateRefreshed -= SystemHelper_WindowsUpdateRefreshed;
                        _systemHelper.ProgressReported -= SystemHelper_ProgressReported;
                    }
                }
                _disposed = true;
            }
        }
        
    }
}