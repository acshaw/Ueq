using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using UnityEngine;

/// <summary>
/// Server-only owner of all runtime DB access. Writes go through a background worker thread
/// so they never stall Mirror's main-thread tick; loads run off-thread and their results are
/// marshaled back to the main thread. Created on server start, flushed + stopped on server stop.
///
/// Iron rule: the worker thread only ever touches plain snapshots + Npgsql, never Unity/Mirror
/// objects. Anything entity-specific is copied into a plain snapshot on the main thread at
/// enqueue time, and load results resume on the main thread before any Unity object is touched.
/// </summary>
public class PersistenceService : MonoBehaviour
{
    public static PersistenceService Instance { get; private set; }

    // Write queue drained by the dedicated worker thread (blocks when empty — no busy-wait).
    readonly BlockingCollection<ISaveJob> _writeQueue =
        new BlockingCollection<ISaveJob>(new ConcurrentQueue<ISaveJob>());

    // Latest pending job per coalesce key. A queued CoalesceMarker resolves to whatever is
    // here at drain time, so repeat saves of one entity collapse to the latest snapshot.
    readonly Dictionary<string, ISaveJob> _pendingByKey = new Dictionary<string, ISaveJob>();
    readonly object _coalesceLock = new object();

    // Continuations to run on the main thread (load callbacks, error notifications).
    readonly ConcurrentQueue<Action> _mainThreadQueue = new ConcurrentQueue<Action>();

    Thread _worker;
    volatile bool _running;

    /// <summary>True while the worker accepts writes. Callers can skip enqueuing during shutdown
    /// (avoids the "EnqueueSave after shutdown" warning on save-on-stop paths).</summary>
    public bool IsRunning => _running;

    const int MaxWriteRetries = 2;          // total attempts = MaxWriteRetries + 1
    const int ShutdownJoinTimeoutMs = 10000;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    public static PersistenceService Create()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("PersistenceService");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<PersistenceService>();
        Instance.StartWorker();
        return Instance;
    }

    void StartWorker()
    {
        _running = true;
        _worker = new Thread(WorkerLoop) { Name = "Ueq-DB-Worker", IsBackground = true };
        _worker.Start();
        Debug.Log("[DB] PersistenceService started (worker thread running).");
    }

    /// <summary>Drain remaining writes, then stop the worker. Safe to call more than once.</summary>
    public void FlushAndStop()
    {
        if (!_running) return;
        _running = false;
        _writeQueue.CompleteAdding();       // worker drains the rest, then exits its loop
        if (_worker != null && _worker.IsAlive && !_worker.Join(ShutdownJoinTimeoutMs))
            Debug.LogWarning("[DB] PersistenceService flush timed out — some writes may not have persisted.");
        DrainMainThreadQueue();             // run any straggler callbacks inline
        Debug.Log("[DB] PersistenceService stopped (queue flushed).");
    }

    void OnApplicationQuit() => FlushAndStop();

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ── Public API (call on the main thread) ──────────────────────────────────

    /// <summary>Enqueue a write. The job must carry a plain snapshot captured on this thread.</summary>
    public void EnqueueSave(ISaveJob job)
    {
        if (!_running)
        {
            Debug.LogWarning("[DB] EnqueueSave after shutdown — ignored.");
            return;
        }

        if (job is IKeyedSaveJob keyed)
        {
            lock (_coalesceLock)
            {
                bool alreadyQueued = _pendingByKey.ContainsKey(keyed.CoalesceKey);
                _pendingByKey[keyed.CoalesceKey] = job;     // always the latest snapshot wins
                if (alreadyQueued) return;                  // a marker is already in the queue
                _writeQueue.Add(new CoalesceMarker(keyed.CoalesceKey));
            }
            return;
        }

        _writeQueue.Add(job);
    }

    /// <summary>Run a read off-thread; invoke <paramref name="onLoaded"/> on the main thread.</summary>
    public void LoadAsync<T>(Func<NpgsqlConnection, T> read, Action<T> onLoaded)
    {
        Task.Run(() =>
        {
            try
            {
                T result;
                using (var conn = Database.OpenConnection())
                    result = read(conn);
                _mainThreadQueue.Enqueue(() => onLoaded(result));
            }
            catch (Exception e)
            {
                _mainThreadQueue.Enqueue(() => Debug.LogError($"[DB] LoadAsync failed: {e.Message}\n{e}"));
            }
        });
    }

    /// <summary>Queue an action to run on the main thread (e.g. from a worker job).</summary>
    public void RunOnMainThread(Action action) => _mainThreadQueue.Enqueue(action);

    void Update() => DrainMainThreadQueue();

    void DrainMainThreadQueue()
    {
        while (_mainThreadQueue.TryDequeue(out var action))
        {
            try { action(); }
            catch (Exception e) { Debug.LogError($"[DB] main-thread callback threw: {e}"); }
        }
    }

    // ── Worker thread ─────────────────────────────────────────────────────────

    void WorkerLoop()
    {
        // GetConsumingEnumerable blocks until items arrive and completes after CompleteAdding + drain.
        foreach (var item in _writeQueue.GetConsumingEnumerable())
        {
            var job = Resolve(item);
            if (job != null) RunJobWithRetry(job);
        }
    }

    ISaveJob Resolve(ISaveJob item)
    {
        if (item is CoalesceMarker marker)
        {
            lock (_coalesceLock)
            {
                if (_pendingByKey.TryGetValue(marker.Key, out var latest))
                {
                    _pendingByKey.Remove(marker.Key);
                    return latest;
                }
                return null;
            }
        }
        return item;
    }

    void RunJobWithRetry(ISaveJob job)
    {
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                using var conn = Database.OpenConnection();
                Database.RunInTransaction(conn, (c, tx) => job.Execute(c, tx));
                return;
            }
            catch (Exception e)
            {
                if (attempt < MaxWriteRetries)
                {
                    Thread.Sleep(100 * (attempt + 1));      // small linear backoff
                    continue;
                }
                _mainThreadQueue.Enqueue(() => Debug.LogError(
                    $"[DB] save job failed after {MaxWriteRetries + 1} attempts, dropped: {e.Message}\n{e}"));
                return;
            }
        }
    }

    /// <summary>Internal queue placeholder that resolves to the latest coalesced job at drain time.</summary>
    sealed class CoalesceMarker : ISaveJob
    {
        public readonly string Key;
        public CoalesceMarker(string key) => Key = key;
        public void Execute(NpgsqlConnection conn, NpgsqlTransaction tx) { } // never executed directly
    }
}
