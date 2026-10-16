using System.Data;
using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Domain.Accounts;
using Xunit.Abstractions;

namespace PersonalTradingJournal.Desktop.Tests.Journals;

public sealed class JournalFixtureLifetimeTests(ITestOutputHelper output)
{
    [Fact]
    public async Task CleanupDrainsSupersededTradeContextReaderBeforeRemovingOwnedDatabase()
    {
        var probe = new HeldCalendarReader();
        var db = await JournalSqliteTests.JournalTestDatabase.CreateAsync(probe);
        Task? older = null, cleanup = null;
        try
        {
            var date = new DateOnly(2026, 10, 7);
            var account = new TradingAccount("Synthetic", TradingAccountType.Personal, null, null, "USD", 0, DateTimeOffset.UtcNow);
            await db.Provider.GetRequiredService<ITradingAccountStore>().AddAsync(account);
            var source = (await db.Repository.CreateAsync(new(date, null, "Original"))).Journal!.Entry;
            await db.Repository.CreateAsync(new(date, account.Id, "Occupied"));
            var vm = await db.OpenEditorAsync(date);
            probe.Arm();
            vm.EditorAccount = vm.Accounts.Single(a => a.Id == account.Id);
            older = vm.TradeContext.LoadTask;
            await probe.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            vm.Text = "Synthetic edited text";
            await vm.SaveCommand.ExecuteAsync(null);
            Assert.Contains("Nothing was moved or merged", vm.ErrorMessage);
            await db.Repository.UpdateAsync(new(source.Id, source.Revision, "Newer", true));
            await vm.SaveCommand.ExecuteAsync(null);
            Assert.Contains("changed elsewhere", vm.ErrorMessage);
            vm.EditorAccount = vm.Accounts.Single(a => a.Id is null);
            await vm.TradeContext.LoadTask;
            Assert.False(vm.SaveCommand.CanExecute(null));
            Assert.True(vm.IsEditorOpen); Assert.Equal("Synthetic edited text", vm.Text);
            Assert.NotSame(older, vm.TradeContext.LoadTask);
            Assert.False(older.IsCompleted);
            Assert.True(probe.Token.IsCancellationRequested);
            Assert.Equal(ConnectionState.Open, probe.Connection!.State);
            Assert.False(probe.Reader!.IsClosed);
            Assert.Equal(db.DatabasePath, probe.Connection.DataSource);
            Assert.Throws<IOException>(() => { using var exclusive = new FileStream(db.DatabasePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None); });
            output.WriteLine($"Owned stale read: context={probe.ContextId}; connection={probe.Connection.State}; readerClosed={probe.Reader.IsClosed}; transaction={probe.HasTransaction}; cancelled={probe.Token.IsCancellationRequested}; olderCompleted={older.IsCompleted}; latestCompleted={vm.TradeContext.LoadTask.IsCompleted}; thread={probe.ThreadId}");
            cleanup = db.DisposeAsync().AsTask();
            await db.Connections.Stopping.Task.WaitAsync(TimeSpan.FromSeconds(10));
            // Old fixture only awaited the latest LoadTask and failed here while OUR reader owned the handle.
            Assert.False(cleanup.IsCompleted, "Fixture must wait for the superseded owned reader, not only the latest load.");
            Assert.Equal(1, db.Connections.ActiveCount);
            await using (var late = await db.ContextFactory.CreateDbContextAsync())
                await Assert.ThrowsAsync<ObjectDisposedException>(() => late.Database.OpenConnectionAsync());
            int executed = probe.Executed;
            probe.Release.TrySetResult();
            await cleanup.WaitAsync(TimeSpan.FromSeconds(10));
            await older.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(probe.Reader.IsClosed);
            Assert.False(Directory.Exists(db.Root));
            Assert.False(File.Exists(db.DatabasePath));
            Assert.False(vm.TradeContext.HasResult); // Cancelled old response did not publish.
            Assert.Equal(0, db.Connections.ActiveCount);
            Assert.Equal(executed, probe.Executed); // No queued query touched the removed database.
            Assert.Throws<ObjectDisposedException>(() => _ = probe.Context!.ChangeTracker);
            output.WriteLine("Cleanup finished after reader disposal; synthetic database/directory removed.");
        }
        finally
        {
            probe.Release.TrySetResult();
            if (older is not null) await older.WaitAsync(TimeSpan.FromSeconds(10));
            if (cleanup is null) await db.DisposeAsync();
            else await cleanup;
        }
    }

    [Theory]
    [InlineData("closed")]
    [InlineData("disposed")]
    [InlineData("failed-open")]
    public async Task ShutdownCoversOpeningFailuresAndDisposalWithoutReleasingOpenHandles(string completion)
    {
        var lifetime = new JournalFixtureConnections();
        using var connection = new SqliteConnection("Data Source=:memory:");
        lifetime.ConnectionOpening(connection, null!, default);
        var drain = lifetime.StopAsync(); // Opening registered before Opened; shutdown must wait.
        Assert.False(drain.IsCompleted);
        using var late = new SqliteConnection("Data Source=:memory:");
        Assert.Throws<ObjectDisposedException>(() => lifetime.ConnectionOpening(late, null!, default));
        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await lifetime.ConnectionOpeningAsync(late, null!, default));
        if (completion == "failed-open")
            await lifetime.ConnectionFailedAsync(connection, null!);
        else
        {
            connection.Open();
            lifetime.ConnectionFailed(connection, null!); // A failed CLOSE with an open handle is not quiescence.
            Assert.False(drain.IsCompleted);
            if (completion == "closed")
            {
                connection.Close();
                await lifetime.ConnectionClosedAsync(connection, null!);
            }
            else
            {
                connection.Dispose();
                await lifetime.ConnectionDisposedAsync(connection, null!);
            }
        }
        await drain.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(0, lifetime.ActiveCount);
        Assert.True(lifetime.StopAsync().IsCompletedSuccessfully);
    }

    private sealed class HeldCalendarReader : DbCommandInterceptor
    {
        private int armed;
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal DbConnection? Connection { get; private set; }
        internal DbDataReader? Reader { get; private set; }
        internal Guid ContextId { get; private set; }
        internal bool HasTransaction { get; private set; }
        internal CancellationToken Token { get; private set; }
        internal int ThreadId { get; private set; }
        internal DbContext? Context { get; private set; }
        private int executed;
        internal int Executed => Volatile.Read(ref executed);
        internal void Arm() => Interlocked.Exchange(ref armed, 1);
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData data,
            DbDataReader result, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref executed);
            if (command.CommandText.Contains("\"TradeBrowse\"", StringComparison.Ordinal) && Interlocked.CompareExchange(ref armed, 0, 1) == 1)
            {
                Connection = command.Connection; Reader = result; ContextId = data.Context!.ContextId.InstanceId;
                Context = data.Context;
                HasTransaction = command.Transaction is not null; Token = cancellationToken; ThreadId = Environment.CurrentManagedThreadId;
                Started.TrySetResult();
                // Controlled SQL completion boundary, not a sleep/retry. Cancellation need not interrupt a native read.
                await Release.Task.WaitAsync(TimeSpan.FromSeconds(10));
            }
            return result;
        }
    }
}
