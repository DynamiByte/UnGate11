using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace UnGate11
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string _status;
        private bool _done = true;
        private int _ticks;

        private const string PipeName = "UnGate11Pipe";
        private CancellationTokenSource _pipeCts;

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
                if (checkResourceName == null)
                {
                    PatchOutputBox.Text = "[ERROR] Embedded check script not found.";
                }
                else
                {
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
                        PatchOutputBox.Text = "[ERROR] Failed to launch check script: " + ex.Message;
                    }
                    // Optionally delete the temp file after a delay
                    // Task.Delay(10000).ContinueWith(_ => { try { File.Delete(tempCheckCmdPath); } catch { } });
                }
            }
            catch (Exception ex)
            {
                PatchOutputBox.Text = "[ERROR] Failed to extract check script: " + ex.Message;
            }

            // Start the named pipe server for status updates from the script
            _pipeCts = new CancellationTokenSource();
            Task.Run(() => ListenPipe(_pipeCts.Token));

            // Animate the patch button as before
            Task.Run(() =>
            {
                while (true)
                {
                    if (!_done)
                    {
                        if (++_ticks > 12)
                            _ticks = 0;

                        string load = _status + ".";
                        if (_ticks > 4) load += ".";
                        if (_ticks > 8) load += ".";

                        Application.Current.Dispatcher.Invoke(DispatcherPriority.Render, new Action(() => PatchButton.Content = load));
                    }
                    Thread.Sleep(100);
                }
            });
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
                                    PatchButton.Content = "Patch";
                                }
                                else if (line == "C0")
                                {
                                    SetStatus("patched");
                                    PatchButton.Content = "Unpatch";
                                }
                                else if (line == "P0")
                                {
                                    SetStatus("patched");
                                    PatchOutputBox.Text = "Patched";
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
                                    PatchOutputBox.Text = "Unpatched";
                                    PatchButton.Content = "Patch Removed";
                                    Task.Run(async () =>
                                    {
                                        await Task.Delay(3000);
                                        Dispatcher.Invoke(() => PatchButton.Content = "Patch");
                                    });
                                }
                                else
                                {
                                    PatchOutputBox.Text = "Unknown status: " + line;
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

            SetStatus("patching");
            PatchOutputBox.Text = "";

            // Extract embedded CMD resource to temp file
            string tempCmdPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Skip_TPM_Check_on_Dynamic_Update.cmd");
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                string resourceName = assembly.GetManifestResourceNames()
                    .FirstOrDefault(n => n.EndsWith("Skip_TPM_Check_on_Dynamic_Update.cmd", StringComparison.OrdinalIgnoreCase));
                if (resourceName == null)
                {
                    PatchOutputBox.Text = "[ERROR] Embedded patch script not found.";
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
                PatchOutputBox.Text = "[ERROR] Failed to extract script: " + ex.Message;
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
                PatchOutputBox.Text = "[ERROR] " + ex.Message;
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

        private Version GetVersion() => Assembly.GetExecutingAssembly().GetName().Version;

        private void CloseWindow(object sender, MouseButtonEventArgs e)
        {
            _pipeCts?.Cancel();
            Application.Current.Shutdown();
        }

        private void DragWindow(object sender, MouseButtonEventArgs e) => DragMove();
    }
}