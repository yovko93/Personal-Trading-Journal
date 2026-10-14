using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace PersonalTradingJournal.Desktop.Tests.Journals;

/// <summary>Fixture-local shutdown barrier. A cancelled/superseded VM task can still own a
/// native SQLite reader; waiting for the newest LoadTask is not resource quiescence.</summary>
internal sealed class JournalFixtureConnections : DbConnectionInterceptor
{
    private readonly object sync = new();
    private readonly HashSet<DbConnection> active = new(ReferenceEqualityComparer.Instance);
    private readonly TaskCompletionSource drained = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool stopping;
    internal TaskCompletionSource Stopping { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal int ActiveCount { get { lock (sync) return active.Count; } }
    internal Task StopAsync()
    {
        lock (sync)
        {
            stopping = true;
            Stopping.TrySetResult();
            if (active.Count == 0) drained.TrySetResult();
            return drained.Task;
        }
    }

    private void Opening(DbConnection connection)
    {
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(stopping, this);
            // Register BEFORE native opening, so stop cannot race an opening that has not
            // yet raised Opened. Future work is rejected before touching the database.
            active.Add(connection);
        }
    }
    private void Released(DbConnection connection)
    {
        lock (sync)
        {
            active.Remove(connection);
            if (stopping && active.Count == 0) drained.TrySetResult();
        }
    }
    public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData data, InterceptionResult result)
    { Opening(connection); return result; }
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData data,
        InterceptionResult result, CancellationToken cancellationToken = default)
    { Opening(connection); return ValueTask.FromResult(result); }
    public override void ConnectionClosed(DbConnection connection, ConnectionEndEventData data) => Released(connection);
    public override Task ConnectionClosedAsync(DbConnection connection, ConnectionEndEventData data)
    { Released(connection); return Task.CompletedTask; }
    public override void ConnectionDisposed(DbConnection connection, ConnectionEndEventData data) => Released(connection);
    public override Task ConnectionDisposedAsync(DbConnection connection, ConnectionEndEventData data)
    { Released(connection); return Task.CompletedTask; }
    public override void ConnectionFailed(DbConnection connection, ConnectionErrorEventData data)
    {
        // A failed close must not falsely report a still-open handle as released.
        if (connection.State == ConnectionState.Closed) Released(connection);
    }
    public override Task ConnectionFailedAsync(DbConnection connection, ConnectionErrorEventData data, CancellationToken cancellationToken = default)
    { ConnectionFailed(connection, data); return Task.CompletedTask; }
}
