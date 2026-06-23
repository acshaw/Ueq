using System;
using Npgsql;

/// <summary>
/// A unit of write work executed on the persistence worker thread. The worker wraps each
/// <see cref="Execute"/> in a transaction (commit on return, rollback + log on throw).
/// A job must only touch its own plain snapshot + the provided connection/transaction —
/// never Unity/Mirror objects (those are captured into the snapshot on the main thread).
/// </summary>
public interface ISaveJob
{
    void Execute(NpgsqlConnection conn, NpgsqlTransaction tx);
}

/// <summary>
/// A save job that can be coalesced: enqueuing a new job replaces any still-pending job
/// with the same <see cref="CoalesceKey"/> (e.g. "character:42"), so rapid repeat saves of
/// one entity collapse to the latest snapshot instead of piling up in the queue.
/// </summary>
public interface IKeyedSaveJob : ISaveJob
{
    string CoalesceKey { get; }
}

/// <summary>Adapts a delegate into a save job — for ad-hoc writes and the DAL self-test.</summary>
public sealed class DelegateSaveJob : ISaveJob
{
    readonly Action<NpgsqlConnection, NpgsqlTransaction> _body;
    public DelegateSaveJob(Action<NpgsqlConnection, NpgsqlTransaction> body) => _body = body;
    public void Execute(NpgsqlConnection conn, NpgsqlTransaction tx) => _body(conn, tx);
}

/// <summary>Coalescing variant of <see cref="DelegateSaveJob"/>.</summary>
public sealed class KeyedDelegateSaveJob : IKeyedSaveJob
{
    readonly Action<NpgsqlConnection, NpgsqlTransaction> _body;
    public string CoalesceKey { get; }

    public KeyedDelegateSaveJob(string coalesceKey, Action<NpgsqlConnection, NpgsqlTransaction> body)
    {
        CoalesceKey = coalesceKey;
        _body = body;
    }

    public void Execute(NpgsqlConnection conn, NpgsqlTransaction tx) => _body(conn, tx);
}
