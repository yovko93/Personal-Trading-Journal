using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Infrastructure.Notebook;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Tests.Backups;

namespace PersonalTradingJournal.Infrastructure.Tests.Notebook;

internal sealed class NotebookFixture : IAsyncDisposable
{
    internal readonly string Root = Directory.CreateTempSubdirectory("ptj-notebook-").FullName;
    internal string DatabasePath => Path.Combine(Root, "journal.db");
    internal readonly NotebookJsonCodec Codec = new();
    internal readonly Clock Time = new();
    internal sealed class Clock : TimeProvider
    {
        internal DateTimeOffset Now = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
    internal IDbContextFactory<JournalDbContext> Factory(params IInterceptor[] interceptors) =>
        new ContextFactory(new DbContextOptionsBuilder<JournalDbContext>()
            .UseSqlite(new SqliteConnectionStringBuilder { DataSource = DatabasePath, Pooling = false, ForeignKeys = true }.ToString())
            .AddInterceptors(interceptors).Options);
    private sealed class ContextFactory(DbContextOptions<JournalDbContext> options) : IDbContextFactory<JournalDbContext>
    {
        public JournalDbContext CreateDbContext() => new(options);
    }
    internal JournalDbContext Context() => Factory().CreateDbContext();
    internal NotebookService Service(params IInterceptor[] interceptors) => new(Factory(interceptors), Codec, Time);
    internal static async Task<NotebookFixture> Create(string? schema = null)
    {
        var f = new NotebookFixture();
        try
        {
            if (schema is null) await MigratedBackupTestDatabase.CreateAsync(f.DatabasePath);
            else { await using var db = f.Context(); await db.GetService<IMigrator>().MigrateAsync(schema); }
            return f;
        }
        catch { await f.DisposeAsync(); throw; }
    }
    internal async Task<NotebookNoteSnapshot> CreateNote(string title = "Untitled", Guid? folder = null) =>
        Success(await Service().ExecuteAsync(new CreateNotebookNote(folder, title)));
    internal static NotebookNoteSnapshot Success(NotebookWriteResult result)
    { Assert.Equal(NotebookWriteStatus.Success, result.Status); return Assert.IsType<NotebookNoteSnapshot>(result.Note); }
    internal async Task<NotebookNamedItem> Folder(string name = "Research")
    {
        var r = await Service().ExecuteCatalogAsync(new CreateNotebookFolder(name));
        Assert.Equal(NotebookWriteStatus.Success, r.Status); return r.Item!;
    }
    internal async Task<NotebookNamedItem> Tag(string name = "NQ")
    {
        var r = await Service().ExecuteCatalogAsync(new CreateNotebookTag(name));
        Assert.Equal(NotebookWriteStatus.Success, r.Status); return r.Item!;
    }
    public ValueTask DisposeAsync() { Directory.Delete(Root, true); return ValueTask.CompletedTask; }
}
