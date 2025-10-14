using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace UnGate11
{
    public class WindowsSystemHelper
    {
        // Events for Status Communication
        // Event to communicate patch status check results
        public event EventHandler<StatusEventArgs> PatchStatusChecked;

        // Event to communicate patch application/removal results
        public event EventHandler<StatusEventArgs> PatchActionCompleted;

        // Event to communicate Windows Update refresh completion
        public event EventHandler<StatusEventArgs> WindowsUpdateRefreshed;

        // Event to communicate progress updates
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
        }
        

        // Native Methods
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool CreateHardLink(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint GetFileAttributes(string lpFileName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetFileAttributes(string lpFileName, uint dwFileAttributes);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetCurrentProcess();

        private const uint FILE_ATTRIBUTE_READONLY = 0x1;
        private const uint FILE_ATTRIBUTE_HIDDEN = 0x2;
        private const uint FILE_ATTRIBUTE_SYSTEM = 0x4;

        // Patch Check and Application
        public async Task CheckPatchStatus()
        {
            try
            {
                ReportProgress("Checking patch status...");

                // Check if patch is applied using Image File Execution Options
                bool isPatchApplied = false;
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\SetupHost.exe\0"))
                {
                    if (key != null && key.GetValue("Debugger") != null)
                    {
                        isPatchApplied = true;
                    }
                }

                // Notify listeners of the result
                await Task.Run(() => RaiseStatusEvent(PatchStatusChecked, isPatchApplied ? "C0" : "C1"));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking patch status: {ex.Message}");
                await Task.Run(() => RaiseStatusEvent(PatchStatusChecked, "C1")); // Default to unpatched on error
            }
        }

        public async Task ApplyPatch()
        {
            try
            {

                // Check current status
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\SetupHost.exe\0"))
                {
                    if (key != null && key.GetValue("Debugger") != null)
                    {
                        ReportProgress("Removing existing patch...");
                        await RemovePatch();
                        return;
                    }
                }

                // Install the patch
                ReportProgress("Creating patch directory...");
                string scriptDir = Path.Combine(Environment.GetEnvironmentVariable("SystemDrive"), "Scripts");
                Directory.CreateDirectory(scriptDir);

                ReportProgress("Writing patch script...");
                string scriptFile = Path.Combine(scriptDir, "get11.cmd");
                string scriptContent = CreatePatchCmdContent();

                // Write the script file
                File.WriteAllText(scriptFile, scriptContent, Encoding.ASCII);

                ReportProgress("Setting up registry entries...");
                // Set registry keys for patch
                using (RegistryKey ifeoKey = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\SetupHost.exe"))
                {
                    ifeoKey.SetValue("UseFilter", 1, RegistryValueKind.DWord);
                }

                using (RegistryKey ifeoSubKey = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\SetupHost.exe\0"))
                {
                    ifeoSubKey.SetValue("FilterFullPath", $@"{Environment.GetEnvironmentVariable("SystemDrive")}\$WINDOWS.~BT\Sources\SetupHost.exe");
                    ifeoSubKey.SetValue("Debugger", scriptFile);
                }

                ReportProgress("Configuring TPM bypass registry keys...");
                // Set TPM bypass registry keys
                using (RegistryKey wuKey = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate"))
                {
                    wuKey.SetValue("DisableWUfBSafeguards", 1, RegistryValueKind.DWord);
                }

                using (RegistryKey moSetupKey = Registry.LocalMachine.CreateSubKey(@"SYSTEM\Setup\MoSetup"))
                {
                    moSetupKey.SetValue("AllowUpgradesWithUnsupportedTPMorCPU", 1, RegistryValueKind.DWord);
                }

                ReportProgress("Patch successfully applied!");
                // Notify that patch was applied
                await Task.Run(() => RaiseStatusEvent(PatchActionCompleted, "P0"));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error applying patch: {ex.Message}");
                ReportProgress($"Error: {ex.Message}");
                await Task.Run(() => RaiseStatusEvent(PatchActionCompleted, "C1"));
            }
        }

        public async Task RemovePatch()
        {
            try
            {

                ReportProgress("Removing patch files...");
                // Remove script files
                string[] scriptPaths = {
                    Path.Combine(Environment.GetEnvironmentVariable("SystemDrive"), "Scripts", "get11.cmd"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments), "get11.cmd"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "get11.cmd")
                };

                foreach (string path in scriptPaths)
                {
                    try
                    {
                        if (File.Exists(path))
                        {
                            File.Delete(path);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error deleting {path}: {ex.Message}");
                    }
                }

                ReportProgress("Removing registry entries...");
                // Remove registry entries
                try
                {
                    Registry.LocalMachine.DeleteSubKeyTree(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\SetupHost.exe", false);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error removing registry key: {ex.Message}");
                }

                ReportProgress("Patch successfully removed!");
                // Notify that patch was removed
                await Task.Run(() => RaiseStatusEvent(PatchActionCompleted, "P1"));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error removing patch: {ex.Message}");
                ReportProgress($"Error: {ex.Message}");
                await Task.Run(() => RaiseStatusEvent(PatchActionCompleted, "C0"));
            }
        }

        private string CreatePatchCmdContent() // Main logic from Skip TPM Check on Dynamic Update V13 by AveYo
        {
            return @"@echo off & title Checking patch status...
            if /i ""%~f0"" neq ""%SystemDrive%\Scripts\get11.cmd"" goto setup
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
                await Task.Run(() => {
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
                        RunProcess("dism", "/cleanup-wim");
                        RunProcess("bitsadmin", "/reset /allusers");

                        // Check if setup files exist and run cleanup
                        string sourcesPath = $@"{systemDrive}\$WINDOWS.~BT\Sources";
                        if (File.Exists($@"{sourcesPath}\setuphost.exe") && File.Exists($@"{sourcesPath}\setupprep.exe"))
                        {
                            ReportProgress("Cleaning setup files...");
                            RunProcess($@"{sourcesPath}\setupprep.exe", "/cleanup /quiet");
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
                                RunProcess(uninstaller, "/ForceUninstall");
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
                            RunProcess("msiexec", $"/X {product} /qn");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error during cleanup: {ex.Message}");
                        ReportProgress($"Error during cleanup: {ex.Message}");
                    }
                });

                // Start services again
                ReportProgress("Restarting Windows Update services...");
                await StartServices(new[] { "bits", "wuauserv", "usosvc" });

                // Refresh settings
                ReportProgress("Refreshing Windows Update settings...");
                RunProcess("UsoClient", "RefreshSettings", false);

                ReportProgress("Windows Update refresh completed successfully!");

                // Notify that Windows Update was refreshed
                await Task.Run(() => RaiseStatusEvent(WindowsUpdateRefreshed, "WUR"));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error refreshing Windows Update: {ex.Message}");
                ReportProgress($"Error: {ex.Message}");
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
                    RunProcess("net", $"stop {service} /y");
                    await Task.Delay(100); // Give time for service to stop
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error stopping {service}: {ex.Message}");
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
                    RunProcess("net", $"start {service} /y");
                    await Task.Delay(100); // Give time for service to start
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error starting {service}: {ex.Message}");
                }
            }
        }

        private async Task KillProcesses(string[] processes)
        {
            foreach (string process in processes)
            {
                try
                {
                    RunProcess("taskkill", $"/f /im {process}.exe");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error killing {process}: {ex.Message}");
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
                Debug.WriteLine($"Error deleting directory {path}: {ex.Message}");
            }
        }

        private Process RunProcess(string fileName, string arguments, bool waitForExit = true)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                var process = Process.Start(psi);
                if (waitForExit && process != null)
                {
                    process.WaitForExit();
                }
                return process;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error running process {fileName} {arguments}: {ex.Message}");
                return null;
            }
        }
        

        // Canary Patching
        public void PatchCanary()
        {
            try
            {
                ReportProgress("Applying canary patch to hwreqchk.dll...");
                string sources = $@"{Environment.GetEnvironmentVariable("SystemDrive")}\$WINDOWS.~BT\Sources";
                string hwreqchkPath = Path.Combine(sources, "hwreqchk.dll");

                if (!File.Exists(hwreqchkPath))
                {
                    ReportProgress("hwreqchk.dll not found, skipping canary patch.");
                    return;
                }

                // Take ownership and set permissions
                ReportProgress("Taking ownership of hwreqchk.dll...");
                RunProcess("takeown.exe", $"/f \"{hwreqchkPath}\" /a");
                RunProcess("icacls.exe", $"\"{hwreqchkPath}\" /grant *S-1-5-32-544:f");

                // Remove read-only and system attributes
                ReportProgress("Modifying file attributes...");
                uint attributes = GetFileAttributes(hwreqchkPath);
                attributes &= ~(FILE_ATTRIBUTE_READONLY | FILE_ATTRIBUTE_SYSTEM);
                SetFileAttributes(hwreqchkPath, attributes);

                // Read file and find patterns
                ReportProgress("Searching for TPM version check pattern...");
                byte[] fileContents = File.ReadAllBytes(hwreqchkPath);

                // Convert patterns to byte arrays
                string originalPattern = "SQ_TpmVersion GTE 1";
                string replacementPattern = "SQ_TpmVersion GTE 0";

                byte[] originalBytes = Encoding.UTF8.GetBytes(originalPattern);
                byte[] replacementBytes = Encoding.UTF8.GetBytes(replacementPattern);

                // Search and replace patterns
                bool modified = false;
                for (int i = 0; i < fileContents.Length - originalBytes.Length; i++)
                {
                    bool match = true;
                    for (int j = 0; j < originalBytes.Length; j++)
                    {
                        if (fileContents[i + j] != originalBytes[j])
                        {
                            match = false;
                            break;
                        }
                    }

                    if (match)
                    {
                        modified = true;
                        for (int j = 0; j < replacementBytes.Length; j++)
                        {
                            fileContents[i + j] = replacementBytes[j];
                        }
                        i += originalBytes.Length;
                    }
                }

                if (modified)
                {
                    // Save the modified file
                    ReportProgress("Patching TPM version check...");
                    File.WriteAllBytes(hwreqchkPath, fileContents);
                    ReportProgress("Canary patch applied successfully!");
                }
                else
                {
                    ReportProgress("TPM version check pattern not found or already patched.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in canary patch: {ex.Message}");
                ReportProgress($"Error applying canary patch: {ex.Message}");
            }
        }
        
    }
}