using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WorkAgent.Config;

namespace WorkAgent.SystemIntegration;

public sealed record UpdateManifest(
    string version,
    string? portalUrl,
    string? downloadUrl,
    string? notes
);

public static class AutoUpdater
{
    private const string VersionManifestUrl = "https://raw.githubusercontent.com/SyedMoosa110/codexplus_Tranking_app_exe/main/version.json";
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private static System.Threading.Timer? _updateTimer;

    public static void StartPeriodicCheck()
    {
        // Check 10 seconds after launch, then every 15 minutes
        _updateTimer = new System.Threading.Timer(
            async _ => await CheckForUpdateAsync(),
            null,
            TimeSpan.FromSeconds(10),
            TimeSpan.FromMinutes(15)
        );
    }

    public static async Task CheckForUpdateAsync()
    {
        try
        {
            var json = await _http.GetStringAsync(VersionManifestUrl);
            var manifest = JsonSerializer.Deserialize<UpdateManifest>(json);
            if (manifest == null || string.IsNullOrWhiteSpace(manifest.version)) return;

            // Update portal URL if specified
            if (!string.IsNullOrWhiteSpace(manifest.portalUrl))
            {
                AgentConfig.SaveServerUrl(manifest.portalUrl);
            }

            // Compare versions
            if (IsNewerVersion(manifest.version, AgentConfig.AppVersion))
            {
                if (!string.IsNullOrWhiteSpace(manifest.downloadUrl))
                {
                    await DownloadAndApplyUpdateAsync(manifest.downloadUrl);
                }
            }
        }
        catch
        {
            // Fail silently so tracking is never interrupted
        }
    }

    private static bool IsNewerVersion(string remote, string local)
    {
        try
        {
            var vRemote = new Version(remote);
            var vLocal = new Version(local);
            return vRemote > vLocal;
        }
        catch
        {
            return string.Compare(remote, local, StringComparison.OrdinalIgnoreCase) > 0;
        }
    }

    private static async Task DownloadAndApplyUpdateAsync(string downloadUrl)
    {
        var currentExePath = Process.GetCurrentProcess().MainModule?.FileName;
        if (string.IsNullOrEmpty(currentExePath)) return;

        var currentDir = Path.GetDirectoryName(currentExePath)!;
        var updateTempPath = Path.Combine(currentDir, "WorkAgent.update.exe");
        var updaterBatPath = Path.Combine(currentDir, "update.bat");

        // 1. Download new binary
        var bytes = await _http.GetByteArrayAsync(downloadUrl);
        if (bytes == null || bytes.Length < 10000) return; // Validation check

        await File.WriteAllBytesAsync(updateTempPath, bytes);

        // 2. Create self-executing batch updater script
        var batContent = $@"@echo off
timeout /t 2 /nobreak >nul
taskkill /f /im WorkAgent.exe >nul 2>&1
timeout /t 1 /nobreak >nul
move /y ""{updateTempPath}"" ""{currentExePath}"" >nul
start """" ""{currentExePath}""
del ""%~f0""
";
        await File.WriteAllTextAsync(updaterBatPath, batContent);

        // 3. Launch batch script hidden and exit current process
        var psi = new ProcessStartInfo
        {
            FileName = updaterBatPath,
            CreateNoWindow = true,
            UseShellExecute = false,
            WindowStyle = ProcessWindowStyle.Hidden
        };

        Process.Start(psi);
        Environment.Exit(0);
    }
}
