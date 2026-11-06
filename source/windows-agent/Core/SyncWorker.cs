using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WorkAgent.Config;
using WorkAgent.Storage;

namespace WorkAgent.Core;

public sealed class SyncWorker : IDisposable
{
    private readonly ApiClient _api;
    private readonly LocalQueue _queue;
    private readonly System.Threading.Timer _timer;
    private bool _isSyncing = false;

    public event Action<bool, int>? OnSyncCompleted;

    public SyncWorker(ApiClient api, LocalQueue queue)
    {
        _api = api;
        _queue = queue;

        // Run sync worker every 30 seconds
        _timer = new System.Threading.Timer(
            async _ => await SyncAsync(),
            null,
            5000,
            AgentConfig.SyncIntervalSec * 1000
        );
    }

    public async Task SyncAsync()
    {
        if (_isSyncing) return;
        _isSyncing = true;

        try
        {
            // 1. Drain local SQLite queue in batches of 300
            var pending = _queue.Peek(300);
            if (pending.Count > 0)
            {
                var ids = pending.Select(p => p.Id).ToList();
                var jsonPayloads = pending.Select(p => p.Json);

                bool success = await _api.SendBatchAsync(jsonPayloads);
                if (success)
                {
                    _queue.Delete(ids);
                    OnSyncCompleted?.Invoke(true, ids.Count);
                }
                else
                {
                    OnSyncCompleted?.Invoke(false, 0);
                }
            }

            // 2. Heartbeat
            await _api.SendHeartbeatAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SyncWorker] Sync failed: {ex.Message}");
            OnSyncCompleted?.Invoke(false, 0);
        }
        finally
        {
            _isSyncing = false;
        }
    }

    public void Dispose()
    {
        _timer.Dispose();
    }
}
