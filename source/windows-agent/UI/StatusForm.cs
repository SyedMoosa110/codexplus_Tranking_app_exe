using System;
using System.Drawing;
using System.Windows.Forms;
using WorkAgent.Core;
using WorkAgent.Storage;

namespace WorkAgent.UI;

public sealed class StatusForm : Form
{
    private readonly ApiClient _api;
    private readonly LocalQueue _queue;
    private readonly SessionManager _session;
    private readonly SyncWorker _sync;

    private readonly Label _statusValue;
    private readonly Label _queueValue;
    private readonly Button _pauseResumeBtn;

    public StatusForm(ApiClient api, LocalQueue queue, SessionManager session, SyncWorker sync)
    {
        _api = api;
        _queue = queue;
        _session = session;
        _sync = sync;

        Text = "Work Agent - Employee Self-Service";
        Width = 420;
        Height = 350;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
        BackColor = Color.White;

        var header = new Label
        {
            Text = "Active Tracking Status",
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Top = 20,
            Left = 25,
            Width = 350
        };

        var empLabel = new Label
        {
            Text = $"Employee: {_api.UserName ?? "Signed In User"}",
            ForeColor = Color.FromArgb(71, 85, 105),
            Top = 55,
            Left = 25,
            Width = 350
        };

        var hostLabel = new Label
        {
            Text = $"Machine: {Environment.MachineName}",
            ForeColor = Color.FromArgb(100, 116, 139),
            Top = 80,
            Left = 25,
            Width = 350
        };

        var statusTitle = new Label { Text = "Current State:", Top = 120, Left = 25, Width = 120 };
        _statusValue = new Label
        {
            Text = _session.IsPaused ? "Paused" : "Tracking Active",
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            ForeColor = _session.IsPaused ? Color.FromArgb(217, 119, 6) : Color.FromArgb(22, 163, 74),
            Top = 118,
            Left = 150,
            Width = 220
        };

        var queueTitle = new Label { Text = "Buffered Events:", Top = 155, Left = 25, Width = 120 };
        _queueValue = new Label
        {
            Text = $"{_queue.GetPendingCount()} pending sync",
            ForeColor = Color.FromArgb(100, 116, 139),
            Top = 155,
            Left = 150,
            Width = 220
        };

        _pauseResumeBtn = new Button
        {
            Text = _session.IsPaused ? "Resume Tracking" : "Take Break / Pause",
            Top = 210,
            Left = 25,
            Width = 170,
            Height = 38,
            BackColor = Color.FromArgb(241, 245, 249),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        _pauseResumeBtn.Click += (s, e) => TogglePause();

        var syncNowBtn = new Button
        {
            Text = "Sync Now",
            Top = 210,
            Left = 210,
            Width = 170,
            Height = 38,
            BackColor = Color.FromArgb(37, 99, 235),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        syncNowBtn.FlatAppearance.BorderSize = 0;
        syncNowBtn.Click += async (s, e) =>
        {
            syncNowBtn.Enabled = false;
            syncNowBtn.Text = "Syncing...";
            await _sync.SyncAsync();
            RefreshValues();
            syncNowBtn.Enabled = true;
            syncNowBtn.Text = "Sync Now";
        };

        Controls.AddRange(new Control[]
        {
            header, empLabel, hostLabel,
            statusTitle, _statusValue,
            queueTitle, _queueValue,
            _pauseResumeBtn, syncNowBtn
        });

        _session.OnStatusChanged += _ => Invoke((Action)RefreshValues);
        _sync.OnSyncCompleted += (_, _) => Invoke((Action)RefreshValues);
    }

    private void TogglePause()
    {
        if (_session.IsPaused)
        {
            _session.ResumeSession();
        }
        else
        {
            _session.PauseSession("EmployeeManualBreak");
        }
        RefreshValues();
    }

    private void RefreshValues()
    {
        _statusValue.Text = _session.IsPaused ? "Paused" : "Tracking Active";
        _statusValue.ForeColor = _session.IsPaused ? Color.FromArgb(217, 119, 6) : Color.FromArgb(22, 163, 74);
        _pauseResumeBtn.Text = _session.IsPaused ? "Resume Tracking" : "Take Break / Pause";
        _queueValue.Text = $"{_queue.GetPendingCount()} pending sync";
    }
}
