using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using WorkAgent.Config;

namespace WorkAgent.Core;

public sealed record LoginResponse(
    string access,
    string refresh,
    string userName,
    string deviceId,
    string organizationId
);

public sealed record PolicyResponse(
    int idleThresholdSec,
    bool captureProcess,
    bool captureWindowTitle,
    bool captureScreenshots,
    int? screenshotEverySec,
    string[] maskedApps,
    object? scheduleJson
);

public sealed record SessionResponse(
    string sessionId,
    string status,
    string startAt
);

public sealed class ApiClient
{
    private readonly HttpClient _http;
    private string? _accessToken;
    private string? _refreshToken;

    public string? UserName { get; private set; }
    public string? DeviceId { get; private set; }

    public ApiClient(string? baseUrl = null)
    {
        _http = new HttpClient
        {
            BaseAddress = new Uri(baseUrl ?? AgentConfig.GetServerUrl()),
            Timeout = TimeSpan.FromSeconds(20)
        };
    }

    public void UpdateBaseUrl(string url)
    {
        if (!url.EndsWith("/")) url += "/";
        _http.BaseAddress = new Uri(url);
    }

    public void SetTokens(string access, string refresh)
    {
        _accessToken = access;
        _refreshToken = refresh;
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", access);
    }

    public async Task<LoginResponse?> LoginAsync(string email, string password, Guid deviceUuid)
    {
        try
        {
            var payload = new
            {
                email,
                password,
                deviceUuid,
                hostname = Environment.MachineName,
                osVersion = Environment.OSVersion.VersionString,
                agentVersion = AgentConfig.AppVersion
            };

            var response = await _http.PostAsJsonAsync("login", payload);
            if (!response.IsSuccessStatusCode) return null;

            var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
            if (result != null)
            {
                SetTokens(result.access, result.refresh);
                UserName = result.userName;
                DeviceId = result.deviceId;
            }
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ApiClient] Login error: {ex.Message}");
            return null;
        }
    }

    public async Task<PolicyResponse?> GetPolicyAsync()
    {
        try
        {
            var response = await _http.GetAsync("policy");
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                if (await TryRefreshTokenAsync())
                {
                    response = await _http.GetAsync("policy");
                }
            }
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<PolicyResponse>();
        }
        catch
        {
            return null;
        }
    }

    public async Task<SessionResponse?> StartSessionAsync()
    {
        try
        {
            var response = await _http.PostAsync("session/start", null);
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized && await TryRefreshTokenAsync())
            {
                response = await _http.PostAsync("session/start", null);
            }
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<SessionResponse>();
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> EndSessionAsync(string reason = "USER")
    {
        try
        {
            var response = await _http.PostAsJsonAsync("session/end", new { reason });
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> SendBatchAsync(IEnumerable<string> rawJsonEvents)
    {
        try
        {
            var jsonBuilder = new StringBuilder();
            jsonBuilder.Append("{\"clientNow\":\"");
            jsonBuilder.Append(DateTime.UtcNow.ToString("o"));
            jsonBuilder.Append("\",\"events\":[");
            jsonBuilder.Append(string.Join(",", rawJsonEvents));
            jsonBuilder.Append("]}");

            using var content = new StringContent(jsonBuilder.ToString(), Encoding.UTF8, "application/json");
            var response = await _http.PostAsync("events/batch", content);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized && await TryRefreshTokenAsync())
            {
                using var retryContent = new StringContent(jsonBuilder.ToString(), Encoding.UTF8, "application/json");
                response = await _http.PostAsync("events/batch", retryContent);
            }

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ApiClient] Batch sync error: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> SendHeartbeatAsync()
    {
        try
        {
            var response = await _http.PostAsync("heartbeat", null);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> TryRefreshTokenAsync()
    {
        if (string.IsNullOrEmpty(_refreshToken)) return false;
        try
        {
            using var client = new HttpClient { BaseAddress = new Uri(_http.BaseAddress!, "../auth/") };
            var res = await client.PostAsJsonAsync("refresh", new { refreshToken = _refreshToken });
            if (!res.IsSuccessStatusCode) return false;

            var data = await res.Content.ReadFromJsonAsync<JsonElement>();
            if (data.TryGetProperty("accessToken", out var acc) && data.TryGetProperty("refreshToken", out var refTok))
            {
                SetTokens(acc.GetString()!, refTok.GetString()!);
                Storage.CredentialStore.SaveTokens(refTok.GetString()!);
                return true;
            }
        }
        catch { }
        return false;
    }
}
