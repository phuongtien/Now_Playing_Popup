using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using WpfApplication = System.Windows.Application;
using WpfMessageBox = System.Windows.MessageBox;
using WpfMessageBoxButton = System.Windows.MessageBoxButton;
using WpfMessageBoxImage = System.Windows.MessageBoxImage;
using WpfMessageBoxResult = System.Windows.MessageBoxResult;
using NowPlayingPopup.UI;


namespace NowPlayingPopup.Services
{
    /// <summary>
    /// Handles application update checking and installation
    /// </summary>
    public class UpdateService
    {
        private readonly Window _mainWindow;
        private const string MANIFEST_URL = "https://raw.githubusercontent.com/phuongtien/Now_Playing_Popup_clean/refs/heads/Tien_main/releases/manifest.json";

        public UpdateService(Window mainWindow)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
        }

        public async Task CheckForUpdatesOnStartupAsync()
        {
            try
            {
                var um = new UpdateManager();
                var manifest = await um.GetRemoteManifestAsync(MANIFEST_URL);

                Debug.WriteLine($"[Update] Manifest fetched: version={manifest?.version} url={manifest?.url}");

                if (manifest == null || string.IsNullOrEmpty(manifest.version) || string.IsNullOrEmpty(manifest.url))
                    return;

                var localVer = UpdateManager.GetLocalVersion();
                Debug.WriteLine($"[Update] Local version: {localVer?.ToString() ?? "<null>"}");

                if (!UpdateManager.IsNewer(manifest.version, localVer))
                {
                    Debug.WriteLine("[Update] No update available.");
                    return;
                }

                if (!await PromptUserForUpdateAsync(manifest))
                    return;

                await DownloadAndInstallUpdateAsync(manifest, um);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Update check failed: {ex.Message}");
            }
        }

        private async Task<bool> PromptUserForUpdateAsync(UpdateManifest manifest)
        {
            var msg = $"Có bản cập nhật {manifest.version}.\n\n{manifest.notes}\n\nBạn có muốn tải và cập nhật bây giờ?";
            var answer = WpfMessageBoxResult.None;

            await WpfApplication.Current.Dispatcher.InvokeAsync(() =>
            {
                answer = WpfMessageBox.Show(msg, "Cập nhật", WpfMessageBoxButton.YesNo, WpfMessageBoxImage.Information);
            });

            return answer == WpfMessageBoxResult.Yes;
        }

        private async Task DownloadAndInstallUpdateAsync(UpdateManifest manifest, UpdateManager um)
        {
            ProgressWindow? progressWindow = await ShowProgressWindowAsync();

            try
            {
                var tmpFile = await DownloadUpdateFileAsync(manifest, progressWindow);

                await CloseProgressWindowAsync(progressWindow);
                progressWindow = null;

                if (tmpFile == null)
                {
                    await ShowDownloadErrorAsync();
                    return;
                }

                if (!await VerifyChecksumAsync(manifest, tmpFile))
                    return;

                UnblockFile(tmpFile);

                if (!await LaunchInstallerAsync(tmpFile))
                {
                    await ShowInstallerErrorAsync(tmpFile);
                }
            }
            finally
            {
                await CloseProgressWindowAsync(progressWindow);
            }
        }

        private async Task<ProgressWindow?> ShowProgressWindowAsync()
        {
            ProgressWindow? progressWindow = null;
            await WpfApplication.Current.Dispatcher.InvokeAsync(() =>
            {
                progressWindow = new ProgressWindow
                {
                    Owner = _mainWindow,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };
                progressWindow.Show();
            });
            return progressWindow;
        }

        private async Task CloseProgressWindowAsync(ProgressWindow? progressWindow)
        {
            if (progressWindow == null) return;

            await WpfApplication.Current.Dispatcher.InvokeAsync(() =>
            {
                progressWindow.Close();
            });
        }

        private async Task<string?> DownloadUpdateFileAsync(UpdateManifest manifest, ProgressWindow? progressWindow)
        {
            var tmpFile = Path.Combine(Path.GetTempPath(), $"NowPlayingPopup_Update_{Guid.NewGuid():N}.exe");

            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
                http.DefaultRequestHeaders.Add("User-Agent", "NowPlayingPopup-Updater");

                using var response = await http.GetAsync(manifest.url!, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var total = response.Content.Headers.ContentLength ?? -1L;
                var canReportProgress = total > 0;

                using var responseStream = await response.Content.ReadAsStreamAsync();
                using var fs = new FileStream(tmpFile, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);

                var buffer = new byte[81920];
                long totalRead = 0;
                int bytesRead;

                while ((bytesRead = await responseStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fs.WriteAsync(buffer, 0, bytesRead);
                    totalRead += bytesRead;

                    if (canReportProgress && progressWindow != null)
                    {
                        int percent = (int)((double)totalRead / total * 100);
                        await WpfApplication.Current.Dispatcher.InvokeAsync(() =>
                        {
                            progressWindow.SetProgress(percent);
                        });
                    }
                }

                await fs.FlushAsync();
                return tmpFile;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Update] Download failed: {ex}");
                try { if (File.Exists(tmpFile)) File.Delete(tmpFile); } catch { }
                return null;
            }
        }

        private async Task ShowDownloadErrorAsync()
        {
            await WpfApplication.Current.Dispatcher.InvokeAsync(() =>
                WpfMessageBox.Show("Tải bản cập nhật thất bại.", "Cập nhật", WpfMessageBoxButton.OK, WpfMessageBoxImage.Error));
        }

        private async Task<bool> VerifyChecksumAsync(UpdateManifest manifest, string tmpFile)
        {
            if (string.IsNullOrWhiteSpace(manifest.sha256))
                return true;

            try
            {
                var computedHash = UpdateManager.ComputeSha256(tmpFile);
                Debug.WriteLine($"[Update] computed sha256: {computedHash}, expected: {manifest.sha256}");

                if (!string.Equals(computedHash, manifest.sha256.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var bad = tmpFile + ".bad";
                        if (File.Exists(bad)) File.Delete(bad);
                        File.Move(tmpFile, bad);
                    }
                    catch { }

                    await WpfApplication.Current.Dispatcher.InvokeAsync(() =>
                        WpfMessageBox.Show("File tải về không khớp checksum. Cập nhật bị hủy.", "Lỗi", WpfMessageBoxButton.OK, WpfMessageBoxImage.Error));
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Update] Checksum verify failed: {ex}");
                try { if (File.Exists(tmpFile)) File.Delete(tmpFile); } catch { }
                return false;
            }
        }

        private async Task<bool> LaunchInstallerAsync(string tmpFile)
        {
            var fileInfo = new FileInfo(tmpFile);
            if (!fileInfo.Exists || fileInfo.Length == 0)
            {
                await WpfApplication.Current.Dispatcher.InvokeAsync(() =>
                    WpfMessageBox.Show("File tải về không hợp lệ.", "Lỗi", WpfMessageBoxButton.OK, WpfMessageBoxImage.Error));
                return false;
            }

            // Try direct launch with retries
            int attempts = 0;
            const int maxAttempts = 8;

            while (attempts < maxAttempts)
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = tmpFile,
                        UseShellExecute = true,
                        Verb = "runas",
                        WorkingDirectory = Path.GetTempPath()
                    };
                    Process.Start(psi);

                    Debug.WriteLine($"[Update] Installer launched (attempt {attempts + 1}).");
                    await ShutdownApplicationAsync();
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Update] Launch attempt {attempts + 1} failed: {ex.Message}");
                    attempts++;
                    await Task.Delay(1000);
                }
            }

            // Fallback: batch script
            return await LaunchInstallerViaBatchAsync(tmpFile);
        }

        private async Task<bool> LaunchInstallerViaBatchAsync(string tmpFile)
        {
            var appProcessName = Process.GetCurrentProcess().ProcessName;
            var batchFile = Path.Combine(Path.GetTempPath(), $"RunInstaller_{Guid.NewGuid():N}.bat");

            var batchContent = $@"@echo off
setlocal
set LOGFILE=%TEMP%\NowPlayingPopup_Update_log_{Guid.NewGuid():N}.txt
echo %DATE% %TIME% - Batch started > ""%LOGFILE%""
echo Temp file: ""{tmpFile}"" >> ""%LOGFILE%""
echo Waiting for app to close... >> ""%LOGFILE%""
:WAIT_LOOP
tasklist /FI ""IMAGENAME eq {appProcessName}.exe"" 2>NUL | find /I ""{appProcessName}.exe"" >NUL
if ""%ERRORLEVEL%""==""0"" (
    timeout /t 1 /nobreak >nul
    goto WAIT_LOOP
)
echo Starting installer... >> ""%LOGFILE%""
powershell -NoProfile -Command ""Start-Process -FilePath '{tmpFile}' -Verb runAs"" >> ""%LOGFILE%"" 2>&1
echo Installer launched. %DATE% %TIME% >> ""%LOGFILE%""
endlocal
exit /b 0
";
            File.WriteAllText(batchFile, batchContent, Encoding.UTF8);

            try
            {
                var psiBatch = new ProcessStartInfo
                {
                    FileName = batchFile,
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetTempPath()
                };
                Process.Start(psiBatch);

                await ShutdownApplicationAsync();
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Update] Failed to start fallback batch: {ex}");
                return false;
            }
        }

        private async Task ShowInstallerErrorAsync(string tmpFile)
        {
            await WpfApplication.Current.Dispatcher.InvokeAsync(() =>
                WpfMessageBox.Show($"Không thể khởi chạy installer tự động.\nVui lòng chạy file thủ công tại:\n{tmpFile}",
                    "Cập nhật", WpfMessageBoxButton.OK, WpfMessageBoxImage.Warning));
        }

        private async Task ShutdownApplicationAsync()
        {
            await WpfApplication.Current.Dispatcher.InvokeAsync(() =>
            {
                WpfApplication.Current.Shutdown();
            });

            _ = Task.Run(async () =>
            {
                await Task.Delay(2000);
                Environment.Exit(0);
            });
        }


        private static void UnblockFile(string filePath)
        {
            try
            {
                // Remove Zone.Identifier
                var zoneIdentifier = filePath + ":Zone.Identifier";
                try { if (File.Exists(zoneIdentifier)) File.Delete(zoneIdentifier); } catch { }

                // Use PowerShell Unblock-File
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-NoProfile -Command \"Unblock-File -Path '{filePath}'\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };
                    var process = Process.Start(psi);
                    process?.WaitForExit(5000);
                }
                catch { }

                // Set normal attributes
                try { File.SetAttributes(filePath, FileAttributes.Normal); } catch { }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UnblockFile failed: {ex.Message}");
            }
        }
    }

    public class UpdateManifest
    {
        public string? name { get; set; }
        public string? version { get; set; }
        public string? notes { get; set; }
        public string? url { get; set; }
        public string? sha256 { get; set; }
        public string? publishedAt { get; set; }
    }

    public class UpdateManager
    {
        private readonly HttpClient _http;

        public UpdateManager()
        {
            _http = new HttpClient() { Timeout = TimeSpan.FromSeconds(30) };
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("NowPlayingPopup-Updater/1.0");
        }

        public async Task<UpdateManifest?> GetRemoteManifestAsync(string manifestUrl)
        {
            try
            {
                var s = await _http.GetStringAsync(manifestUrl);
                return JsonSerializer.Deserialize<UpdateManifest>(s,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch
            {
                return null;
            }
        }

        public static Version? GetLocalVersion()
        {
            try
            {
                var asm = System.Reflection.Assembly.GetEntryAssembly();
                var asmVer = asm?.GetName().Version;
                if (asmVer != null)
                {
                    Debug.WriteLine($"[Update] Assembly version: {asmVer}");
                    return asmVer;
                }

                var path = asm?.Location;
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    var v = FileVersionInfo.GetVersionInfo(path).ProductVersion;
                    if (!string.IsNullOrEmpty(v) && Version.TryParse(NormalizeVersionString(v), out var fv))
                    {
                        Debug.WriteLine($"[Update] FileVersionInfo.ProductVersion: {v} -> parsed {fv}");
                        return fv;
                    }
                }

                try
                {
                    var procPath = Process.GetCurrentProcess().MainModule?.FileName;
                    if (!string.IsNullOrEmpty(procPath) && File.Exists(procPath))
                    {
                        var v = FileVersionInfo.GetVersionInfo(procPath).ProductVersion;
                        if (!string.IsNullOrEmpty(v) && Version.TryParse(NormalizeVersionString(v), out var pv))
                        {
                            Debug.WriteLine($"[Update] Process MainModule version: {pv}");
                            return pv;
                        }
                    }
                }
                catch { }

                Debug.WriteLine("[Update] Could not get local version.");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Update] GetLocalVersion error: {ex}");
                return null;
            }
        }

        public static bool IsNewer(string remoteVersion, Version? local)
        {
            if (local == null) return true;
            if (string.IsNullOrWhiteSpace(remoteVersion)) return false;

            if (!Version.TryParse(NormalizeVersionString(remoteVersion), out var rv))
            {
                Debug.WriteLine($"[Update] Remote version parse failed: '{remoteVersion}'");
                return false;
            }

            Debug.WriteLine($"[Update] Comparing remote {rv} to local {local}");
            return rv > local;
        }

        public static string ComputeSha256(string filePath)
        {
            using var sha = SHA256.Create();
            using var fs = File.OpenRead(filePath);
            var hash = sha.ComputeHash(fs);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        private static string NormalizeVersionString(string s)
        {
            var parts = s.Split('.');
            if (parts.Length == 3) return s + ".0";
            return s;
        }
    }
}