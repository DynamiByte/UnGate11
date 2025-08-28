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
using System.Windows.Threading;

namespace UnGate11
{
    public partial class MainWindow : Window
    {
        private string _status;
        private bool _done = true;
        private int _ticks;

        private const string PipeName = "UnGate11Pipe";
        private CancellationTokenSource _pipeCts;

        // Add this field to track patch status
        private string _currentPatchState = "unpatched"; // Default, will be updated by ListenPipe

        public MainWindow()
        {
            InitializeComponent();
            VersionLabel.Content = $"v{GetVersion().Major}.{GetVersion().Minor}.{GetVersion().Build}";
            SetStatus("ready");

            // Set default text at startup
            PatchButton.Content = "Checking Patch State...";

            // Extract embedded CMD resource to temp file and launch Check_Patch_State.cmd
            string tempCheckCmdPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Check_Patch_State.cmd");
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                string checkResourceName = assembly.GetManifestResourceNames()
                    .FirstOrDefault(n => n.EndsWith("Check_Patch_State.cmd", StringComparison.OrdinalIgnoreCase));
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

        // Left click: Run patcher
        private void PatchButton_Left(object sender, MouseButtonEventArgs e)
        {
            if (!_done) return;

            // Set button content to "Patching" or "Unpatching" based on current state
            if (_currentPatchState == "unpatched")
                PatchButton.Content = "Patching";
            else
                PatchButton.Content = "Unpatching";

            SetStatus("patching");

            // Extract embedded CMD resource to temp file
            string tempCmdPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Skip_TPM_Check_on_Dynamic_Update.cmd");
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                string resourceName = assembly.GetManifestResourceNames()
                    .FirstOrDefault(n => n.EndsWith("Skip_TPM_Check_on_Dynamic_Update.cmd", StringComparison.OrdinalIgnoreCase));
                if (resourceName == null)
                {
                    SetStatus("done");
                    return;
                }
                using (var stream = assembly.GetManifestResourceStream(resourceName))
                using (var file = new FileStream(tempCmdPath, FileMode.Create, FileAccess.Write))
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
                    FileName = tempCmdPath,
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
                // Task.Delay(10000).ContinueWith(_ => { try { File.Delete(tempCmdPath); } catch { } });
            }

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

            UpdateRefreshButton.Content = "Refreshing";

            // Extract embedded CMD resource to temp file
            string tempCmdPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "windows_update_refresh.bat");
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                string resourceName = assembly.GetManifestResourceNames()
                    .FirstOrDefault(n => n.EndsWith("windows_update_refresh.bat", StringComparison.OrdinalIgnoreCase));
                if (resourceName == null)
                {
                    SetStatus("done");
                    return;
                }
                using (var stream = assembly.GetManifestResourceStream(resourceName))
                using (var file = new FileStream(tempCmdPath, FileMode.Create, FileAccess.Write))
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
                    FileName = tempCmdPath,
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
                // Task.Delay(10000).ContinueWith(_ => { try { File.Delete(tempCmdPath); } catch { } });
            }

            SetStatus("done");
        }

        // smae bae
        private void UpdateRefreshButton_Right(object sender, MouseButtonEventArgs e)
        {
            // No-op, but required for XAML compatibility
        }

        private Version GetVersion() => Assembly.GetExecutingAssembly().GetName().Version;

        private void CloseWindow(object sender, MouseButtonEventArgs e)
        {
            _pipeCts?.Cancel();
            Application.Current.Shutdown();
        }

        private void DragWindow(object sender, MouseButtonEventArgs e) => DragMove();
    }
}