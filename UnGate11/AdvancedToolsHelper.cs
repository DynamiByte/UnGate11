using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace UnGate11
{
    public class AdvancedToolsHelper
    {
        public event EventHandler<string> ProgressReported;
        public event EventHandler<string> OutputReceived;
        public event EventHandler<List<string>> EditionsFound;

        // P/Invoke declarations for SPP (Software Protection Platform) API
        [DllImport("slc.dll", CharSet = CharSet.Unicode)]
        private static extern int SLGetWindowsInformationDWORD([MarshalAs(UnmanagedType.LPWStr)] string pwszValueName, out uint pdwValue);

        private void ReportProgress(string message)
        {
            ProgressReported?.Invoke(this, message);
        }

        private void ReportOutput(string output)
        {
            OutputReceived?.Invoke(this, output);
        }

        // Change Windows Edition functionality
        public async Task ChangeWindowsEdition()
        {
            await Task.Run(() =>
            {
                try
                {
                    ReportOutput("=== Change Windows Edition Tool ===");
                    ReportOutput("");

                    // Get current edition info
                    var currentEdition = GetCurrentWindowsEdition();
                    var osVersion = GetWindowsVersion();
                    var osSku = GetWindowsSku();

                    ReportOutput($"Current Edition: {currentEdition}");
                    ReportOutput($"OS Version: {osVersion}");
                    ReportOutput($"SKU ID: {osSku}");
                    ReportOutput("");

                    // Check if running on server
                    if (IsWindowsServer())
                    {
                        ReportOutput("Warning: Running on Windows Server edition.");
                        ReportOutput("Edition changes on Server require different procedures.");
                        ReportOutput("");
                    }

                    // Get target editions
                    ReportOutput("Checking available target editions...");
                    var targetEditions = GetTargetEditions();

                    if (targetEditions.Count == 0)
                    {
                        ReportOutput("No target editions available for upgrade.");
                        ReportOutput("Your current edition may already be at the highest level,");
                        ReportOutput("or edition upgrade is not supported on your system.");
                    }
                    else
                    {
                        ReportOutput($"Found {targetEditions.Count} available target edition(s):");
                        ReportOutput("");
                        
                        // Raise event with editions for UI to display as buttons
                        EditionsFound?.Invoke(this, targetEditions);
                        
                        for (int i = 0; i < targetEditions.Count; i++)
                        {
                            ReportOutput($"  • {targetEditions[i]}");
                        }
                        ReportOutput("");
                        ReportOutput("Select an edition button below to change your Windows edition.");
                        ReportOutput("");
                        ReportOutput("WARNING: Changing editions requires:");
                        ReportOutput("  • A valid product key for the target edition");
                        ReportOutput("  • System restart after the change");
                        ReportOutput("  • Backup of important data is recommended");
                    }

                    // Check activation status for current edition
                    ReportOutput("");
                    ReportOutput("Current Edition Activation Status:");
                    CheckCurrentActivation();
                }
                catch (Exception ex)
                {
                    ReportOutput($"Error: {ex.Message}");
                }
            });
        }

        // Perform the actual edition change
        public async Task<bool> PerformEditionChange(string targetEdition, string productKey)
        {
            return await Task.Run(() =>
            {
                try
                {
                    ReportOutput("");
                    ReportOutput($"=== Changing Edition to {targetEdition} ===");
                    ReportOutput("");
                    
                    if (string.IsNullOrWhiteSpace(productKey))
                    {
                        ReportOutput("Error: Product key is required for edition change.");
                        return false;
                    }

                    ReportOutput("Starting edition change process...");
                    ReportOutput("This may take several minutes. Please wait...");
                    ReportOutput("");

                    var psi = new ProcessStartInfo
                    {
                        FileName = "DISM.exe",
                        Arguments = $"/online /Set-Edition:{targetEdition} /ProductKey:{productKey} /AcceptEula",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        Verb = "runas"
                    };

                    using (var process = Process.Start(psi))
                    {
                        if (process != null)
                        {
                            string output = process.StandardOutput.ReadToEnd();
                            string error = process.StandardError.ReadToEnd();
                            process.WaitForExit();

                            if (!string.IsNullOrWhiteSpace(output))
                            {
                                ReportOutput(output);
                            }

                            if (!string.IsNullOrWhiteSpace(error))
                            {
                                ReportOutput($"Error output: {error}");
                            }

                            if (process.ExitCode == 0)
                            {
                                ReportOutput("");
                                ReportOutput("SUCCESS: Edition change completed!");
                                ReportOutput("");
                                ReportOutput("IMPORTANT: You must restart your computer for the changes to take effect.");
                                ReportOutput("Would you like to restart now?");
                                return true;
                            }
                            else
                            {
                                ReportOutput("");
                                ReportOutput($"ERROR: Edition change failed with exit code {process.ExitCode}");
                                ReportOutput("");
                                ReportOutput("Common reasons for failure:");
                                ReportOutput("  • Invalid or incorrect product key");
                                ReportOutput("  • Target edition is not compatible");
                                ReportOutput("  • Insufficient permissions (run as administrator)");
                                ReportOutput("  • System files are corrupted");
                                return false;
                            }
                        }
                    }

                    ReportOutput("Error: Failed to start DISM process.");
                    return false;
                }
                catch (Exception ex)
                {
                    ReportOutput($"Error during edition change: {ex.Message}");
                    return false;
                }
            });
        }

        // Check Activation Status functionality
        public async Task CheckActivationStatus()
        {
            await Task.Run(() =>
            {
                try
                {
                    ReportOutput("=== Windows Activation Status ===");
                    ReportOutput("");

                    // Get OS info
                    var osName = GetOSName();
                    var osVersion = GetWindowsVersion();
                    var osSku = GetWindowsSku();
                    var osArchitecture = Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit";

                    ReportOutput($"OS: {osName}");
                    ReportOutput($"Version: {osVersion}");
                    ReportOutput($"Architecture: {osArchitecture}");
                    ReportOutput($"SKU ID: {osSku}");
                    ReportOutput("");

                    // Check activation using WMI
                    CheckWindowsActivation();

                    // Check Office activation if installed
                    ReportOutput("");
                    ReportOutput("=== Office Activation Status ===");
                    CheckOfficeActivation();

                }
                catch (Exception ex)
                {
                    ReportOutput($"Error: {ex.Message}");
                }
            });
        }

        // Helper methods
        private string GetCurrentWindowsEdition()
        {
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                {
                    return key?.GetValue("EditionID")?.ToString() ?? "Unknown";
                }
            }
            catch
            {
                return "Unknown";
            }
        }

        private string GetWindowsVersion()
        {
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                {
                    var displayVersion = key?.GetValue("DisplayVersion")?.ToString();
                    if (!string.IsNullOrEmpty(displayVersion))
                        return displayVersion;

                    var releaseId = key?.GetValue("ReleaseId")?.ToString();
                    return releaseId ?? "Unknown";
                }
            }
            catch
            {
                return "Unknown";
            }
        }

        private int GetBuildNumber()
        {
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                {
                    var buildStr = key?.GetValue("CurrentBuild")?.ToString();
                    if (int.TryParse(buildStr, out int build))
                        return build;
                }
            }
            catch
            {
            }
            return 0;
        }

        private uint GetWindowsSku()
        {
            try
            {
                if (SLGetWindowsInformationDWORD("Kernel-BrandingInfo", out uint sku) == 0)
                {
                    return sku;
                }
            }
            catch
            {
            }

            // Fallback to WMI
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT OperatingSystemSKU FROM Win32_OperatingSystem"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        return Convert.ToUInt32(obj["OperatingSystemSKU"]);
                    }
                }
            }
            catch
            {
            }

            return 0;
        }

        private bool IsWindowsServer()
        {
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                {
                    var productName = key?.GetValue("ProductName")?.ToString() ?? "";
                    return productName.Contains("Server");
                }
            }
            catch
            {
                return false;
            }
        }

        private string GetOSName()
        {
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                {
                    return key?.GetValue("ProductName")?.ToString() ?? "Unknown";
                }
            }
            catch
            {
                return "Unknown";
            }
        }

        private List<string> GetTargetEditions()
        {
            var editions = new List<string>();
            
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "DISM.exe",
                    Arguments = "/online /english /Get-TargetEditions",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(psi))
                {
                    if (process != null)
                    {
                        string output = process.StandardOutput.ReadToEnd();
                        process.WaitForExit();

                        // Parse output for "Target Edition :"
                        var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var line in lines)
                        {
                            if (line.Contains("Target Edition :"))
                            {
                                var parts = line.Split(':');
                                if (parts.Length > 1)
                                {
                                    var edition = parts[1].Trim();
                                    if (!string.IsNullOrEmpty(edition))
                                        editions.Add(edition);
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // If DISM fails, return empty list
            }

            return editions;
        }

        private void CheckCurrentActivation()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM SoftwareLicensingProduct WHERE ApplicationID='55c92734-d682-4d71-983e-d6ec3f16059f' AND PartialProductKey <> null"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        var name = obj["Name"]?.ToString();
                        var licenseStatus = Convert.ToInt32(obj["LicenseStatus"]);
                        var licenseStatusStr = GetLicenseStatusString(licenseStatus);
                        var partialKey = obj["PartialProductKey"]?.ToString();
                        var gracePeriod = Convert.ToUInt32(obj["GracePeriodRemaining"]);

                        ReportOutput($"Product: {name}");
                        ReportOutput($"License Status: {licenseStatusStr}");
                        ReportOutput($"Partial Product Key: {partialKey}");
                        
                        if (gracePeriod > 0)
                        {
                            var days = gracePeriod / 1440;
                            ReportOutput($"Grace Period Remaining: {days} days ({gracePeriod} minutes)");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ReportOutput($"Error checking activation: {ex.Message}");
            }
        }

        private void CheckWindowsActivation()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM SoftwareLicensingProduct WHERE ApplicationID='55c92734-d682-4d71-983e-d6ec3f16059f' AND PartialProductKey <> null"))
                {
                    bool foundActivation = false;
                    foreach (var obj in searcher.Get())
                    {
                        foundActivation = true;
                        var name = obj["Name"]?.ToString();
                        var description = obj["Description"]?.ToString();
                        var licenseStatus = Convert.ToInt32(obj["LicenseStatus"]);
                        var licenseStatusStr = GetLicenseStatusString(licenseStatus);
                        var partialKey = obj["PartialProductKey"]?.ToString();
                        var gracePeriod = Convert.ToUInt32(obj["GracePeriodRemaining"]);

                        ReportOutput($"Name: {name}");
                        ReportOutput($"Description: {description}");
                        ReportOutput($"License Status: {licenseStatusStr}");
                        ReportOutput($"Partial Product Key: {partialKey}");
                        
                        if (gracePeriod > 0)
                        {
                            var days = gracePeriod / 1440;
                            ReportOutput($"Grace Period: {days} days ({gracePeriod} minutes)");
                        }
                        else if (licenseStatus == 1)
                        {
                            ReportOutput("The machine is permanently activated.");
                        }

                        ReportOutput("");
                    }

                    if (!foundActivation)
                    {
                        ReportOutput("No Windows activation information found.");
                        ReportOutput("Product key may not be installed.");
                    }
                }
            }
            catch (Exception ex)
            {
                ReportOutput($"Error: {ex.Message}");
            }
        }

        private void CheckOfficeActivation()
        {
            try
            {
                // Check for Office 2016/2019/2021/365 (GUID: 0ff1ce15-a989-479d-af46-f275c6370663)
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM SoftwareLicensingProduct WHERE ApplicationID='0ff1ce15-a989-479d-af46-f275c6370663' AND PartialProductKey <> null"))
                {
                    bool foundOffice = false;
                    foreach (var obj in searcher.Get())
                    {
                        foundOffice = true;
                        var name = obj["Name"]?.ToString();
                        var licenseStatus = Convert.ToInt32(obj["LicenseStatus"]);
                        var licenseStatusStr = GetLicenseStatusString(licenseStatus);
                        var partialKey = obj["PartialProductKey"]?.ToString();

                        ReportOutput($"Product: {name}");
                        ReportOutput($"License Status: {licenseStatusStr}");
                        ReportOutput($"Partial Product Key: {partialKey}");
                        ReportOutput("");
                    }

                    if (!foundOffice)
                    {
                        ReportOutput("No Office products found or activated.");
                    }
                }
            }
            catch (Exception ex)
            {
                ReportOutput($"Error checking Office: {ex.Message}");
            }
        }

        private ActivationInfo GetActivationStatus()
        {
            var info = new ActivationInfo();
            
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM SoftwareLicensingProduct WHERE ApplicationID='55c92734-d682-4d71-983e-d6ec3f16059f' AND PartialProductKey <> null"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        var licenseStatus = Convert.ToInt32(obj["LicenseStatus"]);
                        info.LicenseStatus = GetLicenseStatusString(licenseStatus);
                        info.IsActivated = (licenseStatus == 1);
                        info.GracePeriodRemaining = Convert.ToUInt32(obj["GracePeriodRemaining"]);
                        break;
                    }
                }
            }
            catch
            {
                info.IsActivated = false;
                info.LicenseStatus = "Unknown";
            }

            return info;
        }

        private string GetLicenseStatusString(int status)
        {
            switch (status)
            {
                case 0: return "Unlicensed";
                case 1: return "Licensed";
                case 2: return "OOBGrace";
                case 3: return "OOTGrace";
                case 4: return "NonGenuineGrace";
                case 5: return "Notification";
                case 6: return "ExtendedGrace";
                default: return $"Unknown ({status})";
            }
        }

        private bool CheckInternetConnection()
        {
            try
            {
                using (var client = new System.Net.WebClient())
                {
                    using (client.OpenRead("http://www.microsoft.com"))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                return false;
            }
        }

        // HWID Activation functionality
        public async Task HwidActivate()
        {
            await Task.Run(() =>
            {
                try
                {
                    ReportOutput("=== HWID Activation Tool ===");
                    ReportOutput("");

                    // 1. Pre-checks
                    ReportProgress("Running pre-activation checks...");
                    if (GetActivationStatus().IsActivated)
                    {
                        ReportOutput("Windows is already permanently activated.");
                        return;
                    }
                    if (!CheckInternetConnection())
                    {
                        ReportOutput("Error: Internet connection is required for HWID activation.");
                        return;
                    }
                    ReportOutput("Pre-activation checks passed.");
                    ReportOutput("");

                    // 2. Get OS Info
                    var osSku = GetWindowsSku();
                    var osEdition = GetCurrentWindowsEdition();
                    ReportOutput($"Detected OS: {osEdition} (SKU: {osSku})");

                    // 3. Find Product Key
                    ReportProgress("Finding appropriate product key...");
                    var keyInfo = GetProductKeyForSku(osSku);
                    if (keyInfo == null)
                    {
                        ReportOutput($"Error: This product (SKU: {osSku}) does not support HWID activation.");
                        return;
                    }
                    ReportOutput($"Found key for {keyInfo.EditionName}.");

                    // 4. Install Product Key
                    ReportProgress("Installing product key...");
                    if (!InstallProductKey(keyInfo.Key))
                    {
                        ReportOutput("Error: Failed to install product key. Aborting.");
                        return;
                    }
                    ReportOutput("Product key installed successfully.");
                    ReportOutput("");

                    // 5. Generate Genuine Ticket
                    ReportProgress("Generating GenuineTicket.xml...");
                    string ticketXml = GenerateGenuineTicket(keyInfo.SkuId, keyInfo.KeyPartNumber);
                    if (string.IsNullOrEmpty(ticketXml))
                    {
                        ReportOutput("Error: Failed to generate GenuineTicket. Aborting.");
                        return;
                    }
                    string ticketDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Microsoft\Windows\ClipSVC\GenuineTicket");
                    Directory.CreateDirectory(ticketDir);
                    string ticketPath = Path.Combine(ticketDir, "GenuineTicket.xml");
                    File.WriteAllText(ticketPath, ticketXml, Encoding.ASCII);
                    ReportOutput("GenuineTicket.xml generated successfully.");

                    // 6. Install Genuine Ticket
                    ReportProgress("Installing genuine ticket...");
                    if (!InstallGenuineTicket())
                    {
                        ReportOutput("Error: Failed to install genuine ticket. The system may not be activated.");
                    }
                    else
                    {
                        ReportOutput("Genuine ticket installed successfully.");
                    }
                    ReportOutput("");

                    // 7. Attempt Activation
                    ReportProgress("Attempting online activation...");
                    if (ActivateOnline())
                    {
                        ReportOutput("Activation successful!");
                    }
                    else
                    {
                        ReportOutput("Online activation command sent. Status will update shortly.");
                    }

                    // 8. Final Status Check
                    ReportProgress("Verifying final activation status...");
                    Task.Delay(5000).Wait(); // Wait for activation to process
                    var finalStatus = GetActivationStatus();
                    ReportOutput($"Final License Status: {finalStatus.LicenseStatus}");
                    if (finalStatus.IsActivated)
                    {
                        ReportOutput("SUCCESS: Windows is permanently activated with a digital license.");
                    }
                    else
                    {
                        ReportOutput("WARNING: Activation may not have been successful. Please check activation status again later.");
                    }
                }
                catch (Exception ex)
                {
                    ReportOutput($"An unexpected error occurred: {ex.Message}");
                    ReportOutput(ex.ToString());
                }
            });
        }

        private KeyInfo GetProductKeyForSku(uint skuId)
        {
            // This is a small subset of the keys from the script.
            // A more complete implementation would parse the entire table.
            var keyTable = new List<KeyInfo>
            {
                new KeyInfo { SkuId = 48, Key = "VK7JG-NPHTM-C97JM-9MPGT-3V66T", EditionName = "Professional", KeyPartNumber = "X19-98841" },
                new KeyInfo { SkuId = 101, Key = "YTMG3-N6DKC-DKB77-7M9GH-8HVX7", EditionName = "Core", KeyPartNumber = "X19-98868" },
                new KeyInfo { SkuId = 121, Key = "YNMGQ-8RYV3-4PGQ3-C8XTP-7CFBY", EditionName = "Education", KeyPartNumber = "X19-98886" },
                new KeyInfo { SkuId = 4, Key = "XGVPP-NMH47-7TTHJ-W3FW7-8HV2C", EditionName = "Enterprise", KeyPartNumber = "X19-99683" },
            };

            return keyTable.FirstOrDefault(k => k.SkuId == skuId);
        }

        private bool InstallProductKey(string productKey)
        {
            try
            {
                var managementClass = new ManagementClass("SoftwareLicensingService");
                var managementObject = managementClass.GetInstances().OfType<ManagementObject>().FirstOrDefault();
                if (managementObject == null)
                {
                    ReportOutput("Error: SoftwareLicensingService WMI class not found.");
                    return false;
                }

                var inParams = managementObject.GetMethodParameters("InstallProductKey");
                inParams["ProductKey"] = productKey;
                var outParams = managementObject.InvokeMethod("InstallProductKey", inParams, null);

                return (uint)outParams["ReturnValue"] == 0;
            }
            catch (Exception ex)
            {
                ReportOutput($"Error installing product key: {ex.Message}");
                return false;
            }
        }

        private string GenerateGenuineTicket(uint skuId, string keyPartNumber)
        {
            try
            {
                string sessionIdStr = $"OSMajorVersion=5;OSMinorVersion=1;OSPlatformId=2;PP=0;Pfn=Microsoft.Windows.{skuId}.{keyPartNumber}_8wekyb3d8bbwe;PKeyIID=465145217131314304264339481117862266242033457260311819664735280;";
                string sessionId = Convert.ToBase64String(Encoding.Unicode.GetBytes(sessionIdStr + char.MinValue));
                string propertiesStr = $"OA3xOriginalProductId=;OA3xOriginalProductKey=;SessionId={sessionId};TimeStampClient=2022-10-11T12:00:00Z";

                byte[] keyBlob = new byte[] {
                    0x07,0x02,0x00,0x00,0x00,0xA4,0x00,0x00,0x52,0x53,0x41,0x32,0x00,0x04,0x00,0x00,
                    0x01,0x00,0x01,0x00,0x29,0x87,0xBA,0x3F,0x52,0x90,0x57,0xD8,0x12,0x26,0x6B,0x38,
                    0xB2,0x3B,0xF9,0x67,0x08,0x4F,0xDD,0x8B,0xF5,0xE3,0x11,0xB8,0x61,0x3A,0x33,0x42,
                    0x51,0x65,0x05,0x86,0x1E,0x00,0x41,0xDE,0xC5,0xDD,0x44,0x60,0x56,0x3D,0x14,0x39,
                    0xB7,0x43,0x65,0xE9,0xF7,0x2B,0xA5,0xF0,0xA3,0x65,0x68,0xE9,0xE4,0x8B,0x5C,0x03,
                    0x2D,0x36,0xFE,0x28,0x4C,0xD1,0x3C,0x3D,0xC1,0x90,0x75,0xF9,0x6E,0x02,0xE0,0x58,
                    0x97,0x6A,0xCA,0x80,0x02,0x42,0x3F,0x6C,0x15,0x85,0x4D,0x83,0x23,0x6A,0x95,0x9E,
                    0x38,0x52,0x59,0x38,0x6A,0x99,0xF0,0xB5,0xCD,0x53,0x7E,0x08,0x7C,0xB5,0x51,0xD3,
                    0x8F,0xA3,0x0D,0xA0,0xFA,0x8D,0x87,0x3C,0xFC,0x59,0x21,0xD8,0x2E,0xD9,0x97,0x8B,
                    0x40,0x60,0xB1,0xD7,0x2B,0x0A,0x6E,0x60,0xB5,0x50,0xCC,0x3C,0xB1,0x57,0xE4,0xB7,
                    0xDC,0x5A,0x4D,0xE1,0x5C,0xE0,0x94,0x4C,0x5E,0x28,0xFF,0xFA,0x80,0x6A,0x13,0x53,
                    0x52,0xDB,0xF3,0x04,0x92,0x43,0x38,0xB9,0x1B,0xD9,0x85,0x54,0x7B,0x14,0xC7,0x89,
                    0x16,0x8A,0x4B,0x82,0xA1,0x08,0x02,0x99,0x23,0x48,0xDD,0x75,0x9C,0xC8,0xC1,0xCE,
                    0xB0,0xD7,0x1B,0xD8,0xFB,0x2D,0xA7,0x2E,0x47,0xA7,0x18,0x4B,0xF6,0x29,0x69,0x44,
                    0x30,0x33,0xBA,0xA7,0x1F,0xCE,0x96,0x9E,0x40,0xE1,0x43,0xF0,0xE0,0x0D,0x0A,0x32,
                    0xB4,0xEE,0xA1,0xC3,0x5E,0x9B,0xC7,0x7F,0xF5,0x9D,0xD8,0xF2,0x0F,0xD9,0x8F,0xAD,
                    0x75,0x0A,0x00,0xD5,0x25,0x43,0xF7,0xAE,0x51,0x7F,0xB7,0xDE,0xB7,0xAD,0xFB,0xCE,
                    0x83,0xE1,0x81,0xFF,0xDD,0xA2,0x77,0xFE,0xEB,0x27,0x1F,0x10,0xFA,0x82,0x37,0xF4,
                    0x7E,0xCC,0xE2,0xA1,0x58,0xC8,0xAF,0x1D,0x1A,0x81,0x31,0x6E,0xF4,0x8B,0x63,0x34,
                    0xF3,0x05,0x0F,0xE1,0xCC,0x15,0xDC,0xA4,0x28,0x7A,0x9E,0xEB,0x62,0xD8,0xD8,0x8C,
                    0x85,0xD7,0x07,0x87,0x90,0x2F,0xF7,0x1C,0x56,0x85,0x2F,0xEF,0x32,0x37,0x07,0xAB,
                    0xB0,0xE6,0xB5,0x02,0x19,0x35,0xAF,0xDB,0xD4,0xA2,0x9C,0x36,0x80,0xC6,0xDC,0x82,
                    0x08,0xE0,0xC0,0x5F,0x3C,0x59,0xAA,0x4E,0x26,0x03,0x29,0xB3,0x62,0x58,0x41,0x59,
                    0x3A,0x37,0x43,0x35,0xE3,0x9F,0x34,0xE2,0xA1,0x04,0x97,0x12,0x9D,0x8C,0xAD,0xF7,
                    0xFB,0x8C,0xA1,0xA2,0xE9,0xE4,0xEF,0xD9,0xC5,0xE5,0xDF,0x0E,0xBF,0x4A,0xE0,0x7A,
                    0x1E,0x10,0x50,0x58,0x63,0x51,0xE1,0xD4,0xFE,0x57,0xB0,0x9E,0xD7,0xDA,0x8C,0xED,
                    0x7D,0x82,0xAC,0x2F,0x25,0x58,0x0A,0x58,0xE6,0xA4,0xF4,0x57,0x4B,0xA4,0x1B,0x65,
                    0xB9,0x4A,0x87,0x46,0xEB,0x8C,0x0F,0x9A,0x48,0x90,0xF9,0x9F,0x76,0x69,0x03,0x72,
                    0x77,0xEC,0xC1,0x42,0x4C,0x87,0xDB,0x0B,0x3C,0xD4,0x74,0xEF,0xE5,0x34,0xE0,0x32,
                    0x45,0xB0,0xF8,0xAB,0xD5,0x26,0x21,0xD7,0xD2,0x98,0x54,0x8F,0x64,0x88,0x20,0x2B,
                    0x14,0xE3,0x82,0xD5,0x2A,0x4B,0x8F,0x4E,0x35,0x20,0x82,0x7E,0x1B,0xFE,0xFA,0x2C,
                    0x79,0x6C,0x6E,0x66,0x94,0xBB,0x0A,0xEB,0xBA,0xD9,0x70,0x61,0xE9,0x47,0xB5,0x82,
                    0xFC,0x18,0x3C,0x66,0x3A,0x09,0x2E,0x1F,0x61,0x74,0xCA,0xCB,0xF6,0x7A,0x52,0x37,
                    0x1D,0xAC,0x8D,0x63,0x69,0x84,0x8E,0xC7,0x70,0x59,0xDD,0x2D,0x91,0x1E,0xF7,0xB1,
                    0x56,0xED,0x7A,0x06,0x9D,0x5B,0x33,0x15,0xDD,0x31,0xD0,0xE6,0x16,0x07,0x9B,0xA5,
                    0x94,0x06,0x7D,0xC1,0xE9,0xD6,0xC8,0xAF,0xB4,0x1E,0x2D,0x88,0x06,0xA7,0x63,0xB8,
                    0xCF,0xC8,0xA2,0x6E,0x84,0xB3,0x8D,0xE5,0x47,0xE6,0x13,0x63,0x8E,0xD1,0x7F,0xD4,
                    0x81,0x44,0x38,0xBF
                };

                using (var rsa = new System.Security.Cryptography.RSACryptoServiceProvider())
                {
                    rsa.ImportCspBlob(keyBlob);
                    var sha256 = new System.Security.Cryptography.SHA256Managed();
                    byte[] propertiesBytes = Encoding.UTF8.GetBytes(propertiesStr);
                    byte[] hash = sha256.ComputeHash(propertiesBytes);
                    byte[] signature = rsa.SignHash(hash, System.Security.Cryptography.CryptoConfig.MapNameToOID("SHA256"));
                    string signatureStr = Convert.ToBase64String(signature);

                    return $@"<?xml version=""1.0"" encoding=""utf-8""?><genuineAuthorization xmlns=""http://www.microsoft.com/DRM/SL/GenuineAuthorization/1.0""><version>1.0</version><genuineProperties origin=""sppclient""><properties>{propertiesStr}</properties><signatures><signature name=""clientLockboxKey"" method=""rsa-sha256"">{signatureStr}</signature></signatures></genuineProperties></genuineAuthorization>";
                }
            }
            catch (Exception ex)
            {
                ReportOutput($"Error generating ticket signature: {ex.Message}");
                return null;
            }
        }

        private bool InstallGenuineTicket()
        {
            // The script uses two methods. We will use the clipup.exe method as it's more direct.
            var psi = new ProcessStartInfo("clipup.exe", "-v -o")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            try
            {
                using (var process = Process.Start(psi))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    if (process.ExitCode != 0)
                    {
                        ReportOutput($"clipup.exe failed. Exit code: {process.ExitCode}");
                        ReportOutput($"Output: {output}");
                        ReportOutput($"Error: {error}");
                        return false;
                    }
                    return true;
                }
            }
            catch (Exception ex)
            {
                ReportOutput($"Error running clipup.exe: {ex.Message}");
                return false;
            }
        }

        private bool ActivateOnline()
        {
            try
            {
                var product = GetWindowsProductObject();
                if (product == null)
                {
                    ReportOutput("Error: Could not find Windows product WMI object to activate.");
                    return false;
                }

                var outParams = product.InvokeMethod("Activate", null, null);
                return (uint)outParams["ReturnValue"] == 0;
            }
            catch (Exception ex)
            {
                ReportOutput($"Error during online activation: {ex.Message}");
                return false;
            }
        }

        private ManagementObject GetWindowsProductObject()
        {
            var query = "SELECT * FROM SoftwareLicensingProduct WHERE ApplicationID='55c92734-d682-4d71-983e-d6ec3f16059f' AND PartialProductKey IS NOT NULL AND LicenseDependsOn IS NULL";
            using (var searcher = new ManagementObjectSearcher(query))
            {
                return searcher.Get().OfType<ManagementObject>().FirstOrDefault();
            }
        }

        private class KeyInfo
        {
            public uint SkuId { get; set; }
            public string Key { get; set; }
            public string EditionName { get; set; }
            public string KeyPartNumber { get; set; }
        }

        private class ActivationInfo
        {
            public bool IsActivated { get; set; }
            public string LicenseStatus { get; set; }
            public uint GracePeriodRemaining { get; set; }
        }
    }
}
