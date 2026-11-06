using System;
using System.IO;
using System.Text.Json;

namespace WorkAgent.Config;

public static class AgentConfig
{
    public const string DefaultApiBaseUrl = "http://localhost:4000/api/v1/agent/";
    public const string AppVersion = "1.0.0";
    public const string MutexName = "WorkAgent.Enterprise.SingleInstance.Mutex";
    public const int DefaultIdleThresholdSec = 180;
    public const int SyncIntervalSec = 30;
    public const int HeartbeatIntervalSec = 45;
    public const int ForegroundCheckIntervalSec = 5;

    public static string GetServerUrl()
    {
        // 1. Check config.json next to EXE
        try
        {
            var exeDir = AppDomain.CurrentDomain.BaseDirectory;
            var configPath = Path.Combine(exeDir, "config.json");
            if (File.Exists(configPath))
            {
                var json = File.ReadAllText(configPath);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("serverUrl", out var prop))
                {
                    var url = prop.GetString();
                    if (!string.IsNullOrWhiteSpace(url))
                    {
                        return url.EndsWith("/") ? url : url + "/";
                    }
                }
            }
        }
        catch { }

        // 2. Check local saved server url in AppData
        try
        {
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WorkAgent",
                "server.url"
            );
            if (File.Exists(appDataPath))
            {
                var saved = File.ReadAllText(appDataPath).Trim();
                if (!string.IsNullOrWhiteSpace(saved))
                {
                    return saved.EndsWith("/") ? saved : saved + "/";
                }
            }
        }
        catch { }

        return DefaultApiBaseUrl;
    }

    public static void SaveServerUrl(string url)
    {
        try
        {
            var appDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WorkAgent"
            );
            Directory.CreateDirectory(appDataFolder);
            File.WriteAllText(Path.Combine(appDataFolder, "server.url"), url);
        }
        catch { }
    }
}
