using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace UnGate11
{
    public class WindowsSystemHelper
    {
        private static readonly string LogPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UnGate11", "logs");
        private static readonly string LogFile = Path.Combine(LogPath, $"unGate11_{DateTime.UtcNow:yyyyMMdd_HHmmss}.log");

        // Events for Status Communication
        public event EventHandler<StatusEventArgs> PatchStatusChecked;
        public event EventHandler<StatusEventArgs> PatchActionCompleted;
        public event EventHandler<StatusEventArgs> WindowsUpdateRefreshed;
        public event EventHandler<ProgressEventArgs> ProgressReported;

        // Status event arguments class
        public class StatusEventArgs : EventArgs
        {
            public string StatusCode { get; }

            public StatusEventArgs(string statusCode)
            {
                StatusCode = statusCode;
            }
        }

        // Progress event arguments class
        public class ProgressEventArgs : EventArgs
        {
            public string Message { get; }

            public ProgressEventArgs(string message)
            {
                Message = message;
            }
        }

        // Helper method to raise events
        private void RaiseStatusEvent(EventHandler<StatusEventArgs> eventHandler, string statusCode)
        {
            eventHandler?.Invoke(this, new StatusEventArgs(statusCode));
        }

        // Helper method to report progress
        private void ReportProgress(string message)
        {
            ProgressReported?.Invoke(this, new ProgressEventArgs(message));
            Log(message);
        }

        private static void EnsureLogDirectory()
        {
            try
            {
                if (!Directory.Exists(LogPath))
                    Directory.CreateDirectory(LogPath);
            }
            catch { }
        }

        private static void Log(string message)
        {
            try
            {
                EnsureLogDirectory();
                File.AppendAllText(LogFile, $"[{DateTime.UtcNow:O}] {message}{Environment.NewLine}");
            }
            catch { }
        }

        private static void LogException(Exception ex, string context = null)
        {
            try
            {
                EnsureLogDirectory();
                File.AppendAllText(LogFile, $"[{DateTime.UtcNow:O}] EX: {context} {ex}\n");
            }
            catch { }
        }

        // Patch Check and Application
        public async Task CheckPatchStatus()
        {
            try
            {
                // Define registry path constant
                const string ifeoPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options";

                // Check if the patch is applied by looking for the Debugger value in the registry
                string setupHostDebuggerPath = $@"{ifeoPath}\SetupHost.exe\0";
                bool isPatchApplied = false;

                using (var key = Registry.LocalMachine.OpenSubKey(setupHostDebuggerPath))
                {
                    if (key != null && key.GetValue("Debugger") != null)
                    {
                        isPatchApplied = true;
                    }
                }

                await Task.Run(() => RaiseStatusEvent(PatchStatusChecked, isPatchApplied ? "C0" : "C1"));
            }
            catch (Exception ex)
            {
                LogException(ex, "CheckPatchStatus");
                await Task.Run(() => RaiseStatusEvent(PatchStatusChecked, "C1")); // Default to unpatched on error
            }
        }

        public async Task ApplyPatch()
        {
            var startTime = DateTime.UtcNow;
            try
            {
                // First check if patch is already applied
                bool isPatchApplied = false;
                const string ifeoPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options";
                string setupHostDebuggerPath = $@"{ifeoPath}\SetupHost.exe\0";

                using (var key = Registry.LocalMachine.OpenSubKey(setupHostDebuggerPath))
                {
                    if (key != null && key.GetValue("Debugger") != null)
                    {
                        isPatchApplied = true;
                    }
                }

                if (isPatchApplied)
                {
                    // Wait a second if the operation was too fast to ensure UI consistency
                    var elapsed = DateTime.UtcNow - startTime;
                    if (elapsed.TotalMilliseconds < 1000)
                        await Task.Delay(1000 - (int)elapsed.TotalMilliseconds);

                    await Task.Run(() => RaiseStatusEvent(PatchActionCompleted, "P0"));
                    return;
                }

                // Get the system drive
                string systemDrive = Environment.GetEnvironmentVariable("SystemDrive");
                string scriptsDir = $@"{systemDrive}\Scripts";
                string scriptPath = $@"{scriptsDir}\get11.cmd";

                // Create Scripts directory if it doesn't exist
                if (!Directory.Exists(scriptsDir))
                {
                    Directory.CreateDirectory(scriptsDir);
                }

                // Create the script file with patch content
                File.WriteAllText(scriptPath, CreatePatchCmdContent());

                // Add registry entries
                using (var baseKey = Registry.LocalMachine.CreateSubKey($@"{ifeoPath}\SetupHost.exe"))
                {
                    if (baseKey != null)
                    {
                        baseKey.SetValue("UseFilter", 1, RegistryValueKind.DWord);
                    }
                }

                using (var subKey = Registry.LocalMachine.CreateSubKey($@"{ifeoPath}\SetupHost.exe\0"))
                {
                    if (subKey != null)
                    {
                        subKey.SetValue("FilterFullPath", $@"{systemDrive}\$WINDOWS.~BT\Sources\SetupHost.exe");
                        subKey.SetValue("Debugger", scriptPath);
                    }
                }

                // Wait a second if the operation was too fast to ensure UI consistency
                var elapsedPatch = DateTime.UtcNow - startTime;
                if (elapsedPatch.TotalMilliseconds < 1000)
                    await Task.Delay(1000 - (int)elapsedPatch.TotalMilliseconds);

                await Task.Run(() => RaiseStatusEvent(PatchActionCompleted, "P0"));
            }
            catch (Exception ex)
            {
                LogException(ex, "ApplyPatch");
                await Task.Run(() => RaiseStatusEvent(PatchActionCompleted, "C1")); // reset to original state on error
            }
        }

        public async Task RemovePatch()
        {
            var startTime = DateTime.UtcNow;
            try
            {
                // Delete script files from all potential locations
                string systemDrive = Environment.GetEnvironmentVariable("SystemDrive");
                string publicFolder = Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments);
                publicFolder = Path.GetDirectoryName(publicFolder); // Get Public folder path
                string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

                string[] scriptPaths = {
                    $@"{systemDrive}\Scripts\get11.cmd",
                    $@"{publicFolder}\get11.cmd",
                    $@"{programData}\get11.cmd"
                };

                foreach (string scriptPath in scriptPaths)
                {
                    if (File.Exists(scriptPath))
                    {
                        File.Delete(scriptPath);
                    }
                }

                // Remove registry keys
                const string ifeoPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options";
                Registry.LocalMachine.DeleteSubKeyTree($@"{ifeoPath}\SetupHost.exe", false);

                // Wait a second if the operation was too fast to ensure UI consistency
                var elapsedUnpatch = DateTime.UtcNow - startTime;
                if (elapsedUnpatch.TotalMilliseconds < 1000)
                    await Task.Delay(1000 - (int)elapsedUnpatch.TotalMilliseconds);

                await Task.Run(() => RaiseStatusEvent(PatchActionCompleted, "P1"));
            }
            catch (Exception ex)
            {
                LogException(ex, "RemovePatch");
                await Task.Run(() => RaiseStatusEvent(PatchActionCompleted, "C0")); // reset to original state on error
            }
        }

        private string CreatePatchCmdContent()
        {
            // SetupHost.exe patch from Skip TPM Check on Dynamic Update V13 by AveYo
            return @"@echo off
powershell -win 1 -nop -c "";""
set CLI=%*& set SOURCES=%SystemDrive%\$WINDOWS.~BT\Sources& set MEDIA=.& set MOD=CLI& set PRE=WUA& set /a VER=11
if not defined CLI (exit /b) else if not exist %SOURCES%\SetupHost.exe (exit /b)
if not exist %SOURCES%\WindowsUpdateBox.exe mklink /h %SOURCES%\WindowsUpdateBox.exe %SOURCES%\SetupHost.exe
reg add HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate /f /v DisableWUfBSafeguards /d 1 /t reg_dword
reg add HKLM\SYSTEM\Setup\MoSetup /f /v AllowUpgradesWithUnsupportedTPMorCPU /d 1 /t reg_dword
set OPT=/Compat IgnoreWarning /MigrateDrivers All /Telemetry Disable
set /a restart_application=0x800705BB & (call set CLI=%%CLI:%1 =%%)
set /a incorrect_parameter=0x80070057 & (set SRV=%CLI:/Product Client =%)
set /a launch_option_error=0xc190010a & (set SRV=%SRV:/Product Server =%)
for %%W in (%CLI%) do if /i %%W == /PreDownload (set MOD=SRV)
for %%W in (%CLI%) do if /i %%W == /InstallFile (set PRE=ISO& set ""MEDIA="") else if not defined MEDIA set ""MEDIA=%%~dpW""
if %VER% == 11 for %%W in (""%MEDIA%appraiserres.dll"") do if exist %%W if %%~zW == 0 set AlreadyPatched=1 & set /a VER=10
if %VER% == 11 findstr /r ""P.r.o.d.u.c.t.V.e.r.s.i.o.n...1.0.\..0.\..2.[2-9]"" %SOURCES%\SetupHost.exe >nul 2>nul || set /a VER=10
if %VER% == 11 if not exist ""%MEDIA%EI.cfg"" (echo;[Channel]>%SOURCES%\EI.cfg & echo;_Default>>%SOURCES%\EI.cfg)
if %VER%_%PRE% == 11_ISO (%SOURCES%\WindowsUpdateBox.exe /Product Server /PreDownload /Quiet %OPT%)
if %VER%_%PRE% == 11_ISO (del /f /q %SOURCES%\appraiserres.dll 2>nul & cd.>%SOURCES%\appraiserres.dll & call :canary)
if %VER%_%MOD% == 11_SRV (set ARG=%OPT% %SRV% /Product Server)
if %VER%_%MOD% == 11_CLI (set ARG=%OPT% %CLI%)
%SOURCES%\WindowsUpdateBox.exe %ARG%
if %errorlevel% == %restart_application% (call :canary & %SOURCES%\WindowsUpdateBox.exe %ARG%)
exit /b
:canary
set C=  $X='%SOURCES%\hwreqchk.dll'; $Y='SQ_TpmVersion GTE 1'; $Z='SQ_TpmVersion GTE 0'; if (test-path $X) { 
set C=%C%  try { takeown.exe /f $X /a; icacls.exe $X /grant *S-1-5-32-544:f; attrib -R -S $X; [io.file]::OpenWrite($X).close() }
set C=%C%  catch { return }; $R=[Text.Encoding]::UTF8.GetBytes($Z); $l=$R.Length; $i=2; $w=!1;
set C=%C%  $B=[io.file]::ReadAllBytes($X); $H=[BitConverter]::ToString($B) -replace '-';
set C=%C%  $S=[BitConverter]::ToString([Text.Encoding]::UTF8.GetBytes($Y)) -replace '-';
set C=%C%  do { $i=$H.IndexOf($S, $i + 2); if ($i -gt 0) { $w=!0; for ($k=0; $k -lt $l; $k++) { $B[$k + $i / 2]=$R[$k] } } }
set C=%C%  until ($i -lt 1); if ($w) { [io.file]::WriteAllBytes($X, $B); [GC]::Collect() } }
if %VER%_%PRE% == 11_ISO powershell -nop -c iex($env:C) >nul 2>nul
exit /b";
        }

        // Windows Update Refresh
        public async Task RefreshWindowsUpdate()
        {
            try
            {
                // Stop services
                ReportProgress("Stopping Windows Update services...");
                await StopServices(new[] { "msiserver", "wuauserv", "bits", "usosvc", "dosvc", "cryptsvc" });

                // Kill processes
                ReportProgress("Terminating update processes...");
                await KillProcesses(new[] {
                    "dism", "setuphost", "tiworker", "usoclient", "sihclient", "wuauclt", "culauncher",
                    "sedlauncher", "osrrb", "ruximics", "ruximih", "disktoast", "eosnotify", "musnotification",
                    "musnotificationux", "musnotifyicon", "monotificationux", "mousocoreworker", "windowsupdatebox",
                    "updateassistant", "updateassistantcheck", "windows10upgrade", "windows10upgraderapp", "systemsettings"
                });

                // Clean up files
                try
                {
                    ReportProgress("Cleaning update logs and cache files...");
                    // Delete update logs
                    string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                    string systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                    string systemDrive = Environment.GetEnvironmentVariable("SystemDrive");

                    DeleteDirectory($@"{programData}\USOShared\Logs");
                    DeleteDirectory($@"{programData}\USOPrivate\UpdateStore");
                    DeleteDirectory($@"{programData}\Microsoft\Network\Downloader");
                    DeleteDirectory($@"{systemRoot}\Logs\WindowsUpdate");
                    DeleteDirectory($@"{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)}\UNP");
                    DeleteDirectory($@"{systemRoot}\SoftwareDistribution");
                    DeleteDirectory($@"{systemDrive}\Windows.old\Cleanup");

                    // Cleanup Windows Update packages
                    ReportProgress("Resetting Windows Update components...");
                    await RunProcessAsync("dism", "/cleanup-wim");
                    await RunProcessAsync("bitsadmin", "/reset /allusers");

                    // Check if setup files exist and run cleanup
                    string sourcesPath = $@"{systemDrive}\$WINDOWS.~BT\Sources";
                    if (File.Exists($@"{sourcesPath}\setuphost.exe") && File.Exists($@"{sourcesPath}\setupprep.exe"))
                    {
                        ReportProgress("Cleaning setup files...");
                        await RunProcessAsync($@"{sourcesPath}\setupprep.exe", "/cleanup /quiet");
                    }

                    // Remove forced upgraders
                    ReportProgress("Removing update assistants...");
                    string[] upgradeUninstallers = {
                        $@"{systemRoot}\UpdateAssistant\Windows10Upgrade.exe",
                        $@"{systemDrive}\Windows10Upgrade\Windows10UpgraderApp.exe"
                    };

                    foreach (string uninstaller in upgradeUninstallers)
                    {
                        if (File.Exists(uninstaller))
                        {
                            await RunProcessAsync(uninstaller, "/ForceUninstall");
                        }
                    }

                    // Remove update remediators using MSI product codes
                    ReportProgress("Removing remediation packages...");
                    string[] msiProducts = {
                        "{1BA1133B-1C7A-41A0-8CBF-9B993E63D296}", // osrss
                        "{8F2D6CEB-BC98-4B69-A5C1-78BED238FE77}", // rempl, ruxim
                        "{0746492E-47B6-4251-940C-44462DFD74BB}", // CUAssistant
                        "{76A22428-2400-4521-96AF-7AC4A6174CA5}"  // UpdateAssistant
                    };

                    foreach (string product in msiProducts)
                    {
                        await RunProcessAsync("msiexec", $"/X {product} /qn");
                    }
                }
                catch (Exception ex)
                {
                    LogException(ex, "RefreshWindowsUpdate - cleanup");
                }

                // Start services again
                ReportProgress("Restarting Windows Update services...");
                await StartServices(new[] { "bits", "wuauserv", "usosvc" });

                // Refresh settings
                ReportProgress("Refreshing Windows Update settings...");
                await RunProcessAsync("UsoClient", "RefreshSettings", false);

                ReportProgress("Windows Update refresh completed successfully!");

                // Notify that Windows Update was refreshed
                await Task.Run(() => RaiseStatusEvent(WindowsUpdateRefreshed, "WUR"));
            }
            catch (Exception ex)
            {
                LogException(ex, "RefreshWindowsUpdate");
                await Task.Run(() => RaiseStatusEvent(WindowsUpdateRefreshed, "WUR")); // Send completion even on error
            }
        }

        private async Task StopServices(string[] services)
        {
            foreach (string service in services)
            {
                try
                {
                    ReportProgress($"Stopping {service} service...");
                    await RunProcessAsync("net", $"stop {service} /y");
                    await Task.Delay(100); // Give time for service to stop
                }
                catch (Exception ex)
                {
                    LogException(ex, $"StopServices - {service}");
                }
            }
        }

        private async Task StartServices(string[] services)
        {
            foreach (string service in services)
            {
                try
                {
                    ReportProgress($"Starting {service} service...");
                    await RunProcessAsync("net", $"start {service} /y");
                    await Task.Delay(100); // Give time for service to start
                }
                catch (Exception ex)
                {
                    LogException(ex, $"StartServices - {service}");
                }
            }
        }

        private async Task KillProcesses(string[] processes)
        {
            foreach (string process in processes)
            {
                try
                {
                    await RunProcessAsync("taskkill", $"/f /im {process}.exe");
                }
                catch (Exception ex)
                {
                    LogException(ex, $"KillProcesses - {process}");
                }
            }
            await Task.Delay(500); // Give time for processes to terminate
        }

        private void DeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }
            catch (Exception ex)
            {
                LogException(ex, $"DeleteDirectory - {path}");
            }
        }

        private async Task<Process> RunProcessAsync(string fileName, string arguments, bool waitForExit = true, CancellationToken cancellationToken = default)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                var process = Process.Start(psi);
                if (process != null)
                {
                    // capture output
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var reader = process.StandardOutput;
                            string line;
                            while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) != null)
                            {
                                Log($"OUT: {fileName} {arguments}: {line}");
                            }
                        }
                        catch (Exception ex)
                        {
                            LogException(ex, $"StdOutRead - {fileName}");
                        }
                    });

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var reader = process.StandardError;
                            string line;
                            while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) != null)
                            {
                                Log($"ERR: {fileName} {arguments}: {line}");
                            }
                        }
                        catch (Exception ex)
                        {
                            LogException(ex, $"StdErrRead - {fileName}");
                        }
                    });

                    if (waitForExit)
                    {
                        using (cancellationToken.Register(() => {
                            try { if (!process.HasExited) process.Kill(); } catch { }
                        }))
                        {
                            await process.WaitForExitAsync();
                        }
                    }
                }
                return process;
            }
            catch (Exception ex)
            {
                LogException(ex, $"RunProcessAsync - {fileName} {arguments}");
                return null;
            }
        }
    }
}