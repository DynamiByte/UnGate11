using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace UnGate11
{
    public partial class MainWindow : Window
    {
        private string _status;
        private bool _done = true;
        private int _ticks;

        private BitmapDecoder _gifDecoder;
        private int _gifFrameIndex = 0;
        private DispatcherTimer _gifTimer;

        private const string PipeName = "UnGate11Pipe";
        private CancellationTokenSource _pipeCts;

        private string _currentPatchState = "unpatched"; // Default, will be updated by ListenPipe

        private event Action<string> PipeLineReceived;

        public MainWindow()
        {
            InitializeComponent();
            VersionLabel.Content = $"v{GetVersion().Major}.{GetVersion().Minor}.{GetVersion().Build}";
            SetStatus("ready");

            this.StateChanged += MainWindow_StateChanged;

            // Set default text at startup
            PatchButton.Content = "Checking Patch State...";

            // Extract embedded CMD resource to temp file and launch Patcher.cmd
            string tempCheckCmdPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Patcher.cmd");
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                string checkResourceName = assembly.GetManifestResourceNames()
                    .FirstOrDefault(n => n.EndsWith("Patcher.cmd", StringComparison.OrdinalIgnoreCase));
                using (var stream = assembly.GetManifestResourceStream(checkResourceName))
                using (var file = new FileStream(tempCheckCmdPath, FileMode.Create, FileAccess.Write))
                {
                    stream.CopyTo(file);
                }
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = tempCheckCmdPath,
                        UseShellExecute = true,
                        CreateNoWindow = false
                    };
                        Process.Start(psi);
                }
                catch (Exception ex)
                {
                }
                // Optionally delete the temp file after a delay
                // Task.Delay(10000).ContinueWith(_ => { try { File.Delete(tempCheckCmdPath); } catch { } });
            }
            catch (Exception ex)
            {
            }

            // Start the named pipe server for status updates from the script
            _pipeCts = new CancellationTokenSource();
            Task.Run(() => ListenPipe(_pipeCts.Token));

        }

        private void ListenPipe(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using (var pipeServer = new NamedPipeServerStream(PipeName, PipeDirection.In))
                    using (var reader = new StreamReader(pipeServer))
                    {
                        pipeServer.WaitForConnection();
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            Dispatcher.Invoke(() =>
                            {
                                PipeLineReceived?.Invoke(line); // Raise event for external handlers

                                if (line == "C1")
                                {
                                    SetStatus("unpatched");
                                    _currentPatchState = "unpatched";
                                    PatchButton.Content = "Patch";
                                }
                                else if (line == "C0")
                                {
                                    SetStatus("patched");
                                    _currentPatchState = "patched";
                                    PatchButton.Content = "Unpatch";
                                }
                                else if (line == "P0")
                                {
                                    // Load ani
                                    ((Storyboard)FindResource("FadeLoadingOut")).Begin();
                                    StopLoadingGIF();
                                    ((Storyboard)FindResource("FadeLogoIn")).Begin();
                                    SetStatus("patched");
                                    _currentPatchState = "patched";
                                    PatchButton.Content = "Patch Applied";
                                    Task.Run(async () =>
                                    {
                                        await Task.Delay(3000);
                                        Dispatcher.Invoke(() => PatchButton.Content = "Unpatch");
                                    });
                                }
                                else if (line == "P1")
                                {
                                    // Load ani
                                    ((Storyboard)FindResource("FadeLoadingOut")).Begin();
                                    StopLoadingGIF();
                                    ((Storyboard)FindResource("FadeLogoIn")).Begin();
                                    SetStatus("unpatched");
                                    _currentPatchState = "unpatched";
                                    PatchButton.Content = "Patch Removed";
                                    Task.Run(async () =>
                                    {
                                        await Task.Delay(3000);
                                        Dispatcher.Invoke(() => PatchButton.Content = "Patch");
                                    });
                                }
                                else if (line == "WUR")
                                {
                                    //Load ani
                                    ((Storyboard)FindResource("FadeLoadingOut")).Begin();
                                    StopLoadingGIF();
                                    ((Storyboard)FindResource("FadeLogoIn")).Begin();
                                    UpdateRefreshButton.Content = "Refreshed";
                                    Task.Run(async () =>
                                    {
                                        await Task.Delay(3000);
                                        Dispatcher.Invoke(() => UpdateRefreshButton.Content = "Refresh Windows Update");
                                    });
                                }
                                else
                                {
                                    PatchButton.Content = "Patch";
                                }
                            });
                        }
                    }
                }
                catch
                {
                    // Optionally log or handle errors
                }
            }
        }

        private void SetStatus(string status)
        {
            //if (status == "done" || status == "ready")
            //{
            //    _done = true;
            //    _status = string.Empty;
            //    _ticks = 0;
            //    Application.Current.Dispatcher.Invoke(DispatcherPriority.Render, new Action(() => PatchButton.Content = "Patch"));

            if ( status == "patched")
            {
                _done = true;
                _status = string.Empty;
                _ticks = 0;
                Application.Current.Dispatcher.Invoke(DispatcherPriority.Render, new Action(() => PatchButton.Content = "Unpatch"));
            }
            else if (status == "unpatched")
            {
                _done = true;
                _status = string.Empty;
                _ticks = 0;
                Application.Current.Dispatcher.Invoke(DispatcherPriority.Render, new Action(() => PatchButton.Content = "Patch"));
            }
            else
            {
                _done = false;
                _status = status;
            }
            Console.WriteLine("[Status] " + status);
        }

        private void StartLoadingGIF()
        {
            // Use temp path for GIF
            string tempGIFPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "load.gif");

            // Extract embedded GIF resource to temp file if not already present
            if (!File.Exists(tempGIFPath))
            {
                var assembly = Assembly.GetExecutingAssembly();
                string gifResourceName = assembly.GetManifestResourceNames()
                    .FirstOrDefault(n => n.EndsWith("load.gif", StringComparison.OrdinalIgnoreCase));
                if (gifResourceName == null)
                {
                    //MessageBox.Show("Could not find load.gif as an embedded resource.");
                    return;
                }
                using (var stream = assembly.GetManifestResourceStream(gifResourceName))
                using (var file = new FileStream(tempGIFPath, FileMode.Create, FileAccess.Write))
                {
                    stream.CopyTo(file);
                }
            }

            // Load GIF from temp file
            if (_gifDecoder == null)
            {
                using (var fileStream = new FileStream(tempGIFPath, FileMode.Open, FileAccess.Read))
                {
                    _gifDecoder = BitmapDecoder.Create(fileStream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                }
            }

            _gifFrameIndex = 0;
            LoadingGIF.Source = _gifDecoder.Frames[_gifFrameIndex];
            LoadingGIF.Visibility = Visibility.Visible;
            LoadingGIF.Opacity = 1;

            if (_gifTimer == null)
            {
                _gifTimer = new DispatcherTimer();
                _gifTimer.Interval = TimeSpan.FromMilliseconds(24);
                _gifTimer.Tick += (s, e) =>
                {
                    _gifFrameIndex = (_gifFrameIndex + 1) % _gifDecoder.Frames.Count;
                    LoadingGIF.Source = _gifDecoder.Frames[_gifFrameIndex];
                };
            }
            _gifTimer.Start();
        }
        private void StopLoadingGIF()
        {
            if (_gifTimer != null)
                _gifTimer.Stop();
            LoadingGIF.Visibility = Visibility.Collapsed;
            LoadingGIF.Opacity = 0;
        }

        // Left click: Run patcher

        private async void PatchButton_Left(object sender, MouseButtonEventArgs e)
        {
            if (!_done) return;

            // Fade out logo and show loading gif
            ((Storyboard)FindResource("FadeLogoOut")).Begin();
            ((Storyboard)FindResource("FadeLoadingIn")).Begin();
            StartLoadingGIF();

            // Set button content to "Patching" or "Unpatching" based on current state
            if (_currentPatchState == "unpatched")
                PatchButton.Content = "Patching";
            else
                PatchButton.Content = "Unpatching";

            SetStatus("patching");

            // Run patch operation asynchronously
            string tempPatcherCmdPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Patcher.cmd");
            await Task.Run(() =>
            {
                try
                {
                    var assembly = Assembly.GetExecutingAssembly();
                    string resourceName = assembly.GetManifestResourceNames()
                        .FirstOrDefault(n => n.EndsWith("Patcher.cmd", StringComparison.OrdinalIgnoreCase));
                    if (resourceName == null)
                    {
                        Dispatcher.Invoke(() => SetStatus("done"));
                        return;
                    }
                    using (var stream = assembly.GetManifestResourceStream(resourceName))
                    using (var file = new FileStream(tempPatcherCmdPath, FileMode.Create, FileAccess.Write))
                    {
                        stream.CopyTo(file);
                    }
                }
                catch (Exception)
                {
                    Dispatcher.Invoke(() => SetStatus("done"));
                    return;
                }

                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = tempPatcherCmdPath,
                        Arguments = "patch",
                        UseShellExecute = true,
                        CreateNoWindow = false
                    };
                    Process.Start(psi);
                }
                catch (Exception)
                {
                }
            });

            // Set up deletion after receiving "P0" or "P1" from pipe
            void DeleteCmdFileAfterPatch(string line)
            {
                if ((line == "P0" || line == "P1") && File.Exists(tempPatcherCmdPath))
                {
                    Task.Run(async () =>
                    {
                        await Task.Delay(1000);
                        try { File.Delete(tempPatcherCmdPath); } catch { }
                    });
                }
            }

            // Attach to ListenPipe event via a delegate
            Action<string> pipeLineHandler = null;
            pipeLineHandler = (line) =>
            {
                DeleteCmdFileAfterPatch(line);
                // Detach after first match
                if (line == "P0" || line == "P1")
                {
                    PipeLineReceived -= pipeLineHandler;
                }
            };

            PipeLineReceived += pipeLineHandler;

            SetStatus("done");
        }

        // Right click: do nothing, but required by XAML
        private void PatchButton_Right(object sender, MouseButtonEventArgs e)
        {
            // No-op, but required for XAML compatibility
        }

        private void UpdateRefreshButton_Left(object sender, MouseButtonEventArgs e)
        {
            if (!_done) return;

            ((Storyboard)FindResource("FadeLogoOut")).Begin();
            ((Storyboard)FindResource("FadeLoadingIn")).Begin();
            StartLoadingGIF();

            UpdateRefreshButton.Content = "Refreshing";

            // Extract embedded CMD resource to temp file
            string tempPatcherCmdPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Refresh.cmd");
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                string resourceName = assembly.GetManifestResourceNames()
                    .FirstOrDefault(n => n.EndsWith("Refresh.cmd", StringComparison.OrdinalIgnoreCase));
                if (resourceName == null)
                {
                    SetStatus("done");
                    return;
                }
                using (var stream = assembly.GetManifestResourceStream(resourceName))
                using (var file = new FileStream(tempPatcherCmdPath, FileMode.Create, FileAccess.Write))
                {
                    stream.CopyTo(file);
                }
            }
            catch (Exception ex)
            {
                SetStatus("done");
                return;
            }

            try
            {
                // Start the CMD file with the same permissions as the app (no elevation, no output redirection)
                var psi = new ProcessStartInfo
                {
                    FileName = tempPatcherCmdPath,
                    UseShellExecute = true,
                    CreateNoWindow = false
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
            }
            finally
            {
                // Optionally delete the temp file after a delay, idk
                // Task.Delay(10000).ContinueWith(_ => { try { File.Delete(tempPatcherCmdPath); } catch { } });
            }

            SetStatus("done");
        }

        // smae bae
        private void UpdateRefreshButton_Right(object sender, MouseButtonEventArgs e)
        {
            // No-op, but required for XAML compatibility
        }

        private Version GetVersion() => Assembly.GetExecutingAssembly().GetName().Version;

        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Normal)
            {
                var fadeInStoryboard = (Storyboard)FindResource("FadeInWindow");
                fadeInStoryboard.Begin(this);
            }
        }

        private void CloseWindow(object sender, MouseButtonEventArgs e)
        {
            var storyboard = (Storyboard)FindResource("SlideOutWindow");
            storyboard.Completed += (s, args) =>
            {
                _pipeCts?.Cancel();
            Application.Current.Shutdown();
            };
            storyboard.Begin(this);
        }

        private void MinimizeWindow(object sender, MouseButtonEventArgs e)
        {
            var storyboard = (Storyboard)FindResource("FadeOutWindow");
            storyboard.Completed += (s, args) =>
            {
                WindowState = WindowState.Minimized;
                Opacity = 1;
                //visible.Begin(this);
            };
            storyboard.Begin(this);
        }
        private void Info(object sender, MouseButtonEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/DynamiByte/UnGate11/blob/master/README.md",
                UseShellExecute = true
            });
        }

        private void DragWindow(object sender, MouseButtonEventArgs e) => DragMove();
    }
}