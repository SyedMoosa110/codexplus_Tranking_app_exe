using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using WorkAgent.Config;
using WorkAgent.Core;
using WorkAgent.Storage;
using WorkAgent.SystemIntegration;
using WorkAgent.UI;

namespace WorkAgent;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WorkAgent");
        Directory.CreateDirectory(logDir);
        var logFile = Path.Combine(logDir, "debug.log");
        void Log(string msg) => File.AppendAllText(logFile, $"[{DateTime.UtcNow:o}] {msg}\n");

        Log("1. Main started");

        try
        {
            ApplicationConfiguration.Initialize();
            Log("2. ApplicationConfiguration.Initialize done");

            // 1. Single Instance Protection using Process Name Check
            var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
            var existingProcesses = System.Diagnostics.Process.GetProcessesByName(currentProcess.ProcessName);
            if (existingProcesses.Length > 1)
            {
                Log("Exit: Process already running");
                MessageBox.Show(
                    "Enterprise Work Agent is already running.",
                    "Work Agent Already Running",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
                return;
            }

            Log("3. Single instance check passed");

            // 2. Enable Autostart on Windows Logon & Automatic Updates
            try { Autostart.Enable(); Log("4. Autostart checked"); } catch (Exception e) { Log($"Autostart notice: {e.Message}"); }
            try { AutoUpdater.StartPeriodicCheck(); Log("4b. AutoUpdater initialized"); } catch (Exception e) { Log($"AutoUpdater notice: {e.Message}"); }

            // 3. Initialize Local SQLite Buffer Queue
            var dbPath = Path.Combine(logDir, "queue.db");
            Log($"5. Initializing queue at {dbPath}");
            var queue = new LocalQueue(dbPath);
            var api = new ApiClient();

            // 4. Check for Existing Stored Credentials
            var storedRefreshToken = CredentialStore.LoadRefreshToken();
            Log($"6. Stored token check: {(string.IsNullOrEmpty(storedRefreshToken) ? "None (Showing Login Form)" : "Found")}");

            if (string.IsNullOrEmpty(storedRefreshToken))
            {
                // Show Employee Login Screen
                using var loginForm = new LoginForm(api);
                Log("7. Showing LoginForm dialog");
                var dialogResult = loginForm.ShowDialog();
                Log($"8. LoginForm closed with result: {dialogResult}, Authenticated: {loginForm.IsAuthenticated}");

                if (dialogResult != DialogResult.OK || !loginForm.IsAuthenticated)
                {
                    Log("User closed without authenticating. Exiting.");
                    return;
                }
            }
            else
            {
                api.SetTokens(string.Empty, storedRefreshToken);
            }

            // 5. Initialize Session & Sync Managers
            Log("9. Initializing SessionManager and SyncWorker");
            var session = new SessionManager(queue);
            var sync = new SyncWorker(api, queue);

            // 6. Fetch Org Policy & Start Work Session Asynchronously
            ThreadPool.QueueUserWorkItem(async _ =>
            {
                try
                {
                    var policy = await api.GetPolicyAsync();
                    if (policy != null)
                    {
                        session.IdleThresholdSec = policy.idleThresholdSec;
                        session.MaskedApps = policy.maskedApps;
                    }

                    var sessionRes = await api.StartSessionAsync();
                    if (sessionRes != null && Guid.TryParse(sessionRes.sessionId, out var sId))
                    {
                        session.StartSession(sId);
                    }
                    else
                    {
                        session.StartSession(Guid.NewGuid());
                    }
                }
                catch (Exception ex)
                {
                    Log($"Session start error: {ex.Message}");
                }
            });

            // 7. Run System Tray Application Context
            Log("10. Starting Application.Run with TrayApp");
            Application.Run(new TrayApp(api, queue, session, sync));
            Log("11. Application.Run ended.");
        }
        catch (Exception ex)
        {
            Log($"FATAL EXCEPTION: {ex}");
            MessageBox.Show(ex.ToString(), "Work Agent Fatal Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
