using System;
using System.Threading;
using Microsoft.Win32;
using WorkAgent.Config;
using WorkAgent.Storage;
using WorkAgent.Tracking;

namespace WorkAgent.Core;

public sealed class SessionManager : IDisposable
{
    private readonly LocalQueue _queue;
    private readonly System.Threading.Timer _tickTimer;
    private Guid _currentSessionId = Guid.Empty;
    private string _lastProcessName = string.Empty;
    private string? _lastWindowTitle = string.Empty;
    private bool _isCurrentlyIdle = false;
    private int _tickCount = 0;

    public bool IsPaused { get; set; } = false;
    public bool CaptureWindowTitle { get; set; } = true;
    public int IdleThresholdSec { get; set; } = AgentConfig.DefaultIdleThresholdSec;
    public string[]? MaskedApps { get; set; }

    public event Action<string>? OnStatusChanged;

    public SessionManager(LocalQueue queue)
    {
        _queue = queue;

        // Register Windows Power & Lock Event Handlers
        SystemEvents.SessionSwitch += HandleSessionSwitch;
        SystemEvents.PowerModeChanged += HandlePowerModeChanged;

        // Tick every 5 seconds
        _tickTimer = new System.Threading.Timer(OnTick, null, 0, AgentConfig.ForegroundCheckIntervalSec * 1000);
    }

    public void StartSession(Guid sessionId)
    {
        _currentSessionId = sessionId;
        IsPaused = false;
        EmitEvent("RESUME");
        OnStatusChanged?.Invoke("Tracking Active");
    }

    public void PauseSession(string reason = "USER")
    {
        IsPaused = true;
        EmitEvent("PAUSE", new { reason });
        OnStatusChanged?.Invoke("Tracking Paused");
    }

    public void ResumeSession()
    {
        IsPaused = false;
        EmitEvent("RESUME");
        OnStatusChanged?.Invoke("Tracking Active");
    }

    private void OnTick(object? state)
    {
        if (IsPaused || _currentSessionId == Guid.Empty) return;

        // 1. Check Idle Status
        var idleDuration = IdleDetector.GetIdleTime();
        bool idleNow = idleDuration.TotalSeconds >= IdleThresholdSec;

        if (idleNow && !_isCurrentlyIdle)
        {
            _isCurrentlyIdle = true;
            EmitEvent("IDLE_START");
            OnStatusChanged?.Invoke("Idle Detected");
        }
        else if (!idleNow && _isCurrentlyIdle)
        {
            _isCurrentlyIdle = false;
            EmitEvent("IDLE_END");
            OnStatusChanged?.Invoke("Tracking Active");
        }

        // 2. Check Foreground Application & Active Window Title
        _tickCount++;
        var app = ForegroundAppTracker.GetCurrentForegroundApp(CaptureWindowTitle, MaskedApps);
        bool appOrTitleChanged = app != null && (app.ProcessName != _lastProcessName || app.WindowTitle != _lastWindowTitle);
        bool periodicMinuteSnapshot = _tickCount % 12 == 0; // Every 60 seconds (12 * 5s)

        if (app != null && (appOrTitleChanged || periodicMinuteSnapshot))
        {
            _lastProcessName = app.ProcessName;
            _lastWindowTitle = app.WindowTitle;
            EmitEvent("APP_CHANGE", new
            {
                processName = app.ProcessName,
                windowTitle = app.WindowTitle
            });
        }
    }

    private void HandleSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        if (e.Reason is SessionSwitchReason.SessionLock or SessionSwitchReason.SessionLogoff)
        {
            EmitEvent("PAUSE", new { reason = e.Reason.ToString() });
            OnStatusChanged?.Invoke("Session Locked");
        }
        else if (e.Reason == SessionSwitchReason.SessionUnlock)
        {
            EmitEvent("RESUME");
            OnStatusChanged?.Invoke("Tracking Active");
        }
    }

    private void HandlePowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Suspend)
        {
            EmitEvent("PAUSE", new { reason = "SystemSuspend" });
            OnStatusChanged?.Invoke("System Sleeping");
        }
        else if (e.Mode == PowerModes.Resume)
        {
            EmitEvent("RESUME");
            OnStatusChanged?.Invoke("Tracking Active");
        }
    }

    public void EmitEvent(string type, object? payload = null)
    {
        if (_currentSessionId == Guid.Empty) return;

        var eventId = Guid.NewGuid();
        var eventObj = new
        {
            id = eventId.ToString(),
            sessionId = _currentSessionId.ToString(),
            type,
            occurredAt = DateTime.UtcNow.ToString("o"),
            payload
        };

        _queue.Enqueue(eventId, eventObj);
    }

    public void Dispose()
    {
        SystemEvents.SessionSwitch -= HandleSessionSwitch;
        SystemEvents.PowerModeChanged -= HandlePowerModeChanged;
        _tickTimer.Dispose();
    }
}
