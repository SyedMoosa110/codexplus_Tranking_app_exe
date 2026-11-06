using System;
using System.Drawing;
using System.Windows.Forms;
using WorkAgent.Core;
using WorkAgent.Storage;

namespace WorkAgent.UI;

public sealed class TrayApp : ApplicationContext
{
    private readonly ApiClient _api;
    private readonly LocalQueue _queue;
    private readonly SessionManager _session;
    private readonly SyncWorker _sync;
    private readonly NotifyIcon _trayIcon;
    private StatusForm? _statusForm;

    public TrayApp(ApiClient api, LocalQueue queue, SessionManager session, SyncWorker sync)
    {
        _api = api;
        _queue = queue;
        _session = session;
        _sync = sync;

        var contextMenu = new ContextMenuStrip();
        var statusItem = contextMenu.Items.Add("Tracking Status...", null, (s, e) => ShowStatus());
        statusItem.Font = new Font(statusItem.Font, FontStyle.Bold);

        var pauseItem = contextMenu.Items.Add("Take Break (Pause)", null, (s, e) => TogglePause(contextMenu));
        contextMenu.Items.Add("Sync Events Now", null, async (s, e) => await _sync.SyncAsync());
        contextMenu.Items.Add(new ToolStripSeparator());

        contextMenu.Items.Add("Sign Out...", null, (s, e) => SignOut());
        contextMenu.Items.Add("Exit", null, (s, e) => ExitApp());

        _trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            ContextMenuStrip = contextMenu,
            Text = "Enterprise Work Agent - Tracking Active",
            Visible = true
        };

        _trayIcon.DoubleClick += (s, e) => ShowStatus();

        _session.OnStatusChanged += (status) =>
        {
            _trayIcon.Text = $"Work Agent - {status}";
        };
    }

    private void ShowStatus()
    {
        if (_statusForm == null || _statusForm.IsDisposed)
        {
            _statusForm = new StatusForm(_api, _queue, _session, _sync);
        }
        _statusForm.Show();
        _statusForm.BringToFront();
    }

    private void TogglePause(ContextMenuStrip menu)
    {
        if (_session.IsPaused)
        {
            _session.ResumeSession();
            menu.Items[1].Text = "Take Break (Pause)";
        }
        else
        {
            _session.PauseSession("ManualPause");
            menu.Items[1].Text = "Resume Tracking";
        }
    }

    private void SignOut()
    {
        var confirm = MessageBox.Show(
            "Are you sure you want to sign out? This will end your active work session.",
            "Confirm Sign Out",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question
        );

        if (confirm == DialogResult.Yes)
        {
            _session.PauseSession("SignOut");
            _api.EndSessionAsync("SIGN_OUT").Wait(2000);
            CredentialStore.ClearCredentials();
            ExitThread();
        }
    }

    private void ExitApp()
    {
        _session.PauseSession("AppExit");
        _api.EndSessionAsync("USER_EXIT").Wait(2000);
        _trayIcon.Visible = false;
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _session.Dispose();
            _sync.Dispose();
        }
        base.Dispose(disposing);
    }
}
