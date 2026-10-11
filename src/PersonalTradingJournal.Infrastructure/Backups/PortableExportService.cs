using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using PersonalTradingJournal.Application.Backups;
using PersonalTradingJournal.Application.Common.Storage;

namespace PersonalTradingJournal.Infrastructure.Backups;

/// <summary>Logical export from one validated snapshot. No live row queries, economics recalculation or provider dependency.</summary>
public sealed class PortableExportService : IPortableExportService
{
    private readonly IApplicationPaths paths;
    private readonly IDatabaseSnapshotService snapshots;
    private readonly Action<PortableExportPhase, string>? checkpoint;
    private readonly ExportLimits limits;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public PortableExportService(IApplicationPaths paths, IDatabaseSnapshotService snapshots) : this(paths, snapshots, null, new()) { }
    internal PortableExportService(IApplicationPaths paths, IDatabaseSnapshotService snapshots,
        Action<PortableExportPhase, string>? checkpoint, ExportLimits limits)
    { this.paths = paths; this.snapshots = snapshots; this.checkpoint = checkpoint; this.limits = limits; }

    public Task<PortableExportResult> CreateAsync(string destinationDirectory, CancellationToken cancellationToken = default) =>
        Task.Run(() => Create(destinationDirectory, cancellationToken), CancellationToken.None);

    private PortableExportResult Create(string destination, CancellationToken token)
    {
        Guid id = Guid.NewGuid(); var phase = PortableExportPhase.Destination;
        var result = new PortableExportResult(id, PortableExportStatus.IoFailure, phase);
        var owned = new List<string>(); var directories = new List<string>();
        var budget = new ExportBudget(limits.OutputBytes); string? root = null; long rows = 0;
        var counts = new SortedDictionary<string, long>(StringComparer.Ordinal);
        void Check(PortableExportPhase next)
        { phase = next; token.ThrowIfCancellationRequested(); checkpoint?.Invoke(next, root!); token.ThrowIfCancellationRequested(); }
        FileStream New(string file)
        { var stream = new FileStream(file, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920); owned.Add(file); return stream; }
        try
        {
            Check(PortableExportPhase.Destination);
            if (!Path.IsPathFullyQualified(destination)) throw new ExportFailure(PortableExportStatus.DestinationUnavailable);
            destination = Path.TrimEndingDirectorySeparator(Path.GetFullPath(destination));
            string data = Path.TrimEndingDirectorySeparator(Path.GetFullPath(paths.DataDirectory));
            if (destination.Equals(data, StringComparison.OrdinalIgnoreCase) || destination.StartsWith(data + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new ExportFailure(PortableExportStatus.DestinationUnavailable);
            SqliteDatabaseSnapshotService.EnsureNoLinks(destination);
            string? parent = Path.GetDirectoryName(destination);
            if (parent is null || !Directory.Exists(parent) || Path.Exists(destination)) throw new ExportFailure(PortableExportStatus.DestinationUnavailable);
            root = Path.Combine(parent, ".ptjexport-" + id.ToString("N") + ".partial");
            if (Path.Exists(root)) throw new ExportFailure(PortableExportStatus.DestinationUnavailable);
            BackupArchiveService.CreatePrivateDirectory(root); directories.Add(root);
            string payload = Path.Combine(root, "output");
            BackupArchiveService.CreatePrivateDirectory(payload); directories.Add(payload);
            Check(PortableExportPhase.Snapshot);
            // Cooperating restore cannot switch this dataset while SQLite captures its read-only snapshot.
            DatabaseSnapshotResult captured;
            using (new JournalDataSession(paths)) captured = snapshots.CreateAsync(root, token).GetAwaiter().GetResult();
            result = result with { SnapshotFailure = captured.Status == DatabaseSnapshotStatus.Success ? null : captured.Status, CleanupFailed = captured.CleanupFailed };
            if (captured.Status != DatabaseSnapshotStatus.Success || captured.CleanupFailed)
                throw new ExportFailure(captured.Status == DatabaseSnapshotStatus.Cancelled ? PortableExportStatus.Cancelled : PortableExportStatus.SnapshotFailed);
            var snapshot = captured.Snapshot!;
            if (Path.GetDirectoryName(snapshot.DirectoryPath) != root || Path.GetDirectoryName(snapshot.DatabasePath) != snapshot.DirectoryPath)
                throw new ExportFailure(PortableExportStatus.InvalidData);
            directories.Add(snapshot.DirectoryPath); owned.Add(snapshot.DatabasePath);
            Check(PortableExportPhase.Read);
            using (var pin = new FileStream(snapshot.DatabasePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (!BackupFileIdentity.IsUnaliased(pin, snapshot.DatabasePath) || pin.Length != snapshot.File.SizeBytes || Hash(pin, token) != snapshot.File.Sha256)
                    throw new ExportFailure(PortableExportStatus.InvalidData);
                using var database = new SqliteConnection(new SqliteConnectionStringBuilder
                { DataSource = snapshot.DatabasePath, Mode = SqliteOpenMode.ReadOnly, Cache = SqliteCacheMode.Private, Pooling = false, DefaultTimeout = 2 }.ToString());
                database.Open();
                SQLitePCL.raw.sqlite3_limit(database.Handle, SQLitePCL.raw.SQLITE_LIMIT_LENGTH, PortableExportContract.MaximumCellBytes * 2);
                SQLitePCL.raw.sqlite3_progress_handler(database.Handle, 1000, _ => token.IsCancellationRequested ? 1 : 0, null);
                using (var queryOnly = database.CreateCommand())
                { queryOnly.CommandText = "PRAGMA query_only=ON; PRAGMA trusted_schema=OFF; PRAGMA cache_size=-4096; PRAGMA temp_store=FILE;"; queryOnly.ExecuteNonQuery(); }
                Check(PortableExportPhase.Write);
                using var file = New(Path.Combine(payload, PortableExportContract.DataFile));
                using (var output = new BudgetStream(file, budget))
                using (var json = new Utf8JsonWriter(output))
                {
                    json.WriteStartObject(); json.WriteString("format", PortableExportContract.Format); json.WriteNumber("version", PortableExportContract.Version);
                    json.WriteString("snapshotCreatedAtUtc", snapshot.CreatedAtUtc); json.WriteString("screenshotContent", PortableExportContract.ScreenshotContent);
                    json.WriteBoolean("restorableBackup", false); json.WritePropertyName("databaseMigrations"); JsonSerializer.Serialize(json, snapshot.Schema.AppliedMigrations);
                    json.WriteStartObject("tables");
                    foreach (var table in PortableExportSchema.Tables)
                    {
                        token.ThrowIfCancellationRequested();
                        using var csvFile = table.Csv ? New(Path.Combine(payload, table.Name + ".csv")) : null;
                        using var csvBudget = csvFile is null ? null : new BudgetStream(csvFile, budget);
                        using var csv = csvBudget is null ? null : new StreamWriter(csvBudget, new UTF8Encoding(false, true), 8192, leaveOpen: true);
                        if (csv is not null) { csv.NewLine = "\r\n"; csv.WriteLine(string.Join(',', table.Columns.Select(c => Quote(c.Name)))); }
                        json.WriteStartArray(table.Name);
                        using var command = database.CreateCommand();
                        // Identifiers are exclusively from the versioned static allowlist, never user data.
                        command.CommandText = "SELECT " + string.Join(',', table.Columns.Select(c => Q(c.Column))) + "," +
                            string.Join(',', table.Columns.Select(c => "length(CAST(" + Q(c.Column) + " AS BLOB))")) +
                            " FROM " + Q(table.Source) + " ORDER BY " + string.Join(',', table.Order.Split(',').Select(Q)) + ";";
                        using var reader = command.ExecuteReader();
                        long tableRows = 0;
                        while (reader.Read())
                        {
                            token.ThrowIfCancellationRequested(); json.WriteStartObject();
                            for (int i = 0; i < table.Columns.Length; i++)
                            {
                                token.ThrowIfCancellationRequested();
                                if (!reader.IsDBNull(i + table.Columns.Length) && reader.GetInt64(i + table.Columns.Length) > limits.CellBytes)
                                    throw new ExportFailure(PortableExportStatus.LimitExceeded);
                                var column = table.Columns[i]; string? value = Value(reader, i, column.Kind);
                                json.WritePropertyName(column.Name);
                                if (value is null) json.WriteNullValue();
                                else if (column.Kind == ExportValueKind.Integer) json.WriteNumberValue(long.Parse(value, CultureInfo.InvariantCulture));
                                else if (column.Kind == ExportValueKind.Boolean) json.WriteBooleanValue(value == "true");
                                else json.WriteStringValue(value);
                                if (csv is not null)
                                {
                                    if (i != 0) csv.Write(',');
                                    csv.Write(value is null ? "null" : Quote(column.Kind == ExportValueKind.Text && value.Length > 0 ? "'" + value : value));
                                }
                                // Bounded by one field, not one complete table/AI history. All rows remain in the snapshot.
                                json.Flush();
                            }
                            json.WriteEndObject(); csv?.Write("\r\n"); tableRows++; rows++;
                            Check(PortableExportPhase.Write);
                        }
                        json.WriteEndArray(); json.Flush(); csv?.Flush(); csvFile?.Flush(true);
                        counts.Add(table.Name, tableRows);
                    }
                    json.WriteEndObject(); json.WriteEndObject(); json.Flush();
                }
                file.Flush(true);
            }
            Check(PortableExportPhase.Inventory);
            var files = owned.Where(f => Path.GetDirectoryName(f) == payload).Select(f =>
            {
                token.ThrowIfCancellationRequested(); using var read = File.OpenRead(f);
                return new ExportFile(Path.GetFileName(f), read.Length, Hash(read, token));
            }).OrderBy(f => f.Path, StringComparer.Ordinal).ToArray();
            using (var manifest = New(Path.Combine(payload, PortableExportContract.InventoryFile)))
            {
                using var output = new BudgetStream(manifest, budget);
                JsonSerializer.Serialize(output, new
                {
                    format = PortableExportContract.Format, version = PortableExportContract.Version, snapshotCreatedAtUtc = snapshot.CreatedAtUtc,
                    unencrypted = true, restorableBackup = false, screenshotContent = PortableExportContract.ScreenshotContent,
                    csv = "UTF-8, RFC4180 quoting, CRLF; null is unquoted null; nonempty free text has a leading apostrophe for spreadsheet display. Not universally safe across programs or re-save. Exact text is in journal.json. Decimal strings are exact; no P&L is recalculated.",
                    exclusions = new[] { "screenshot binaries", "TradeBrowse derived cache", "SQLite database", "credentials", "settings", "logs", "caches" },
                    fields = PortableExportSchema.Tables.ToDictionary(t => t.Name, t => t.Columns.Select(c => new { name = c.Name, kind = c.Kind.ToString() }).ToArray()),
                    rows = counts, files
                }, Json);
                output.Flush(); manifest.Flush(true);
            }
            Check(PortableExportPhase.Cleanup);
            File.Delete(snapshot.DatabasePath); owned.Remove(snapshot.DatabasePath);
            Directory.Delete(snapshot.DirectoryPath); directories.Remove(snapshot.DirectoryPath);
            Check(PortableExportPhase.Publish);
            Directory.Move(payload, destination); // Same-volume publication, no overwrite. No partial final directory.
            owned.RemoveAll(f => Path.GetDirectoryName(f) == payload); directories.Remove(payload);
            result = result with { Status = PortableExportStatus.Success, Phase = phase, Rows = rows, OutputBytes = budget.Written };
        }
        catch (OperationCanceledException) { result = result with { Status = PortableExportStatus.Cancelled, Phase = phase }; }
        catch (ExportFailure e) { result = result with { Status = e.Status, Phase = phase }; }
        catch (SqliteException e) { result = result with { Status = token.IsCancellationRequested ? PortableExportStatus.Cancelled : e.SqliteErrorCode == 18 ? PortableExportStatus.LimitExceeded : PortableExportStatus.InvalidData, Phase = phase }; }
        catch (Exception e) when (e is FormatException or OverflowException or InvalidDataException or InvalidCastException or EncoderFallbackException)
        { result = result with { Status = PortableExportStatus.InvalidData, Phase = phase }; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        { result = result with { Status = phase == PortableExportPhase.Destination ? PortableExportStatus.DestinationUnavailable : PortableExportStatus.IoFailure, Phase = phase }; }
        finally
        {
            foreach (string file in owned)
                try { File.Delete(file); }
                catch (DirectoryNotFoundException) { }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { result = result with { CleanupFailed = true }; }
            foreach (string directory in directories.AsEnumerable().Reverse())
                try { Directory.Delete(directory); }
                catch (DirectoryNotFoundException) { }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { result = result with { CleanupFailed = true }; }
        }
        return result;

    }

    private static string Q(string identifier) => "\"" + identifier + "\"";
    private static string Quote(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
    private static string? Value(SqliteDataReader row, int ordinal, ExportValueKind kind)
    {
        if (row.IsDBNull(ordinal)) return null;
        return kind switch
        {
            ExportValueKind.Id => row.GetGuid(ordinal).ToString("D"),
            ExportValueKind.Decimal => row.GetDecimal(ordinal).ToString(CultureInfo.InvariantCulture),
            ExportValueKind.Integer => row.GetInt64(ordinal).ToString(CultureInfo.InvariantCulture),
            ExportValueKind.Boolean => row.GetInt64(ordinal) switch { 0 => "false", 1 => "true", _ => throw new InvalidDataException() },
            ExportValueKind.Utc => new DateTimeOffset(row.GetDateTime(ordinal).Ticks, TimeSpan.Zero).ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture),
            ExportValueKind.NewYorkDate => DateOnly.ParseExact(row.GetString(ordinal), "yyyy-MM-dd", CultureInfo.InvariantCulture).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            _ => row.GetString(ordinal)
        };
    }
    private static string Hash(Stream stream, CancellationToken token)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); byte[] buffer = new byte[81920]; int read;
        while ((read = stream.Read(buffer)) > 0) { token.ThrowIfCancellationRequested(); hash.AppendData(buffer, 0, read); }
        token.ThrowIfCancellationRequested(); return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
    private sealed record ExportFile(string Path, long SizeBytes, string Sha256);
    private sealed class ExportFailure(PortableExportStatus status) : IOException { internal PortableExportStatus Status => status; }
    private sealed class ExportBudget(long maximum)
    {
        internal long Written { get; private set; }
        internal void Add(int length) { if (length > maximum - Written) throw new ExportFailure(PortableExportStatus.LimitExceeded); Written += length; }
    }
    private sealed class BudgetStream(Stream inner, ExportBudget budget) : Stream
    {
        public override void Write(byte[] buffer, int offset, int count) { budget.Add(count); inner.Write(buffer, offset, count); }
        public override void Write(ReadOnlySpan<byte> buffer) { budget.Add(buffer.Length); inner.Write(buffer); }
        public override void Flush() => inner.Flush();
        public override bool CanRead => false; public override bool CanSeek => false; public override bool CanWrite => true;
        public override long Length => inner.Length; public override long Position { get => inner.Position; set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}

internal sealed record ExportLimits(long OutputBytes = PortableExportContract.MaximumOutputBytes, int CellBytes = PortableExportContract.MaximumCellBytes);
