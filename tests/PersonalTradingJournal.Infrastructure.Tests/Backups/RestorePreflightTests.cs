using System.Collections.Immutable;
using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Backups;
using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Application.DailyReview;
using PersonalTradingJournal.Application.DailyReview.Coaching;
using PersonalTradingJournal.Infrastructure.Backups;
using Fixture = PersonalTradingJournal.Infrastructure.Tests.Backups.BackupArchiveTests.Fixture;

namespace PersonalTradingJournal.Infrastructure.Tests.Backups;

public sealed class RestorePreflightTests
{
    private const string Db = BackupArchiveContract.DatabasePath;
    private const string Manifest = BackupArchiveContract.ManifestPath;
    private const string Shot = BackupArchiveContract.ScreenshotsPrefix + "image.png";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProducerRoundTripReturnsOnlySafeCountsAndCleansEvenSuccessfulStaging(bool populated)
    {
        await using var f = await Fixture.Create();
        if (populated)
        {
            await f.Add("image.png"); await f.Add("other.jpg");
            var repository = f.Services.GetRequiredService<IDailyJournalRepository>();
            var entry = (await repository.CreateAsync(new(new(2026, 10, 10), null, "Synthetic private content"))).Journal!.Entry;
            await repository.UpdateAsync(new(entry.Id, entry.Revision, "Synthetic revised content"));
            var packet = CoachingEvidencePacketBuilder.Build(await f.Services.GetRequiredService<IDailyReviewEvidenceReader>().GetAsync(new(new(2026, 10, 10)))).Packet!;
            var analysis = CoachingAnalysisSnapshot.Create(packet, new(CoachingGenerationStatus.Success,
                new(packet.ContractVersion, packet.PacketId, new("Synthetic summary", ["calculated:day"]), [], [], [], []),
                new("Fake", "test-model", "synthetic-client", null, null, new(1, 0, 1, 2)), "Unused"), DateTimeOffset.UtcNow);
            await f.Services.GetRequiredService<ICoachingAnalysisRepository>().SaveAsync(analysis);
        }
        Assert.True((await f.Service().CreateAsync(f.Output)).Succeeded);
        byte[] original = await File.ReadAllBytesAsync(f.Output);
        string installedHash = InstalledHash(f);
        var result = await f.Services.GetRequiredService<IRestorePreflightService>().InspectAsync(f.Output, f.Root);
        Assert.True(result.Validated, result.ToString()); Assert.False(result.CleanupFailed);
        Assert.Equal(BackupSchemaCompatibility.Exact, result.Compatibility);
        var summary = result.Summary!;
        Assert.Equal(populated ? 2 : 0, summary.AccountCount); Assert.Equal(populated ? 2 : 0, summary.TradeCount);
        Assert.Equal(populated ? 1 : 0, summary.JournalCount); Assert.Equal(populated ? 2 : 0, summary.JournalRevisionCount);
        Assert.Equal(populated ? 2 : 0, summary.ScreenshotCount); Assert.Equal(summary.ScreenshotCount, summary.ScreenshotFileCount);
        Assert.Equal(populated ? 1 : 0, summary.AnalysisCount); Assert.Equal(original.Length, summary.ArchiveBytes);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(original)), summary.ArchiveSha256);
        Assert.Contains(RestorePreflightWarning.UnencryptedSensitiveData, summary.Warnings);
        Assert.Contains(RestorePreflightWarning.AiCredentialsExcluded, summary.Warnings);
        Assert.DoesNotContain(f.Root, result.ToString()); Assert.DoesNotContain("Synthetic", result.ToString());
        Assert.Equal(original, await File.ReadAllBytesAsync(f.Output)); Assert.Equal(installedHash, InstalledHash(f)); Clean(f);
    }

    [Theory]
    [InlineData("hash", BackupValidationCode.ContentMismatch)]
    [InlineData("missing", BackupValidationCode.MissingEntry)]
    [InlineData("extra", BackupValidationCode.UnexpectedEntry)]
    [InlineData("duplicate", BackupValidationCode.DuplicateEntry)]
    [InlineData("case", BackupValidationCode.DuplicateEntry)]
    [InlineData("traversal", BackupValidationCode.UnsafePath)]
    [InlineData("absolute", BackupValidationCode.UnsafePath)]
    [InlineData("link", BackupValidationCode.UnsafePath)]
    [InlineData("directory", BackupValidationCode.UnsafePath)]
    [InlineData("unknown-path", BackupValidationCode.UnexpectedEntry)]
    [InlineData("missing-manifest", BackupValidationCode.MissingEntry)]
    [InlineData("bad-manifest", BackupValidationCode.InvalidManifest)]
    [InlineData("future-archive", BackupValidationCode.UnsupportedArchiveVersion)]
    [InlineData("future-schema", BackupValidationCode.UnsupportedDatabaseSchema)]
    [InlineData("older-schema", BackupValidationCode.UnsupportedDatabaseSchema)]
    [InlineData("declared-size", BackupValidationCode.ContentMismatch)]
    [InlineData("invalid-db", BackupValidationCode.DatabaseIntegrityFailed)]
    [InlineData("wrong-preferences", BackupValidationCode.ContentMismatch)]
    public async Task HostilePayloadsAreRejectedWithoutChangingInstalledFiles(string defect, BackupValidationCode expected)
    {
        await using var f = await Fixture.Create(); await f.Add("image.png");
        Assert.True((await f.Service().CreateAsync(f.Output)).Succeeded);
        var entries = Read(f.Output); var manifest = Parse(entries);
        switch (defect)
        {
            case "hash": entries.Single(e => e.Name == Shot).Bytes[0] ^= 1; break;
            case "missing": entries.RemoveAll(e => e.Name == Shot); break;
            case "extra": entries.Add(new(BackupArchiveContract.ScreenshotsPrefix + "extra.png", [1])); break;
            case "duplicate": entries.Add(entries.Single(e => e.Name == Shot)); break;
            case "case": entries.Add(new(Shot.Replace("image", "IMAGE"), [1])); break;
            case "traversal": entries.Add(new("../escape.png", [1])); break;
            case "absolute": entries.Add(new("C:/escape.png", [1])); break;
            case "link": entries.Add(new(BackupArchiveContract.ScreenshotsPrefix + "link.png", [1], unchecked((int)0xa1ff0000))); break;
            case "directory": entries.Add(new(BackupArchiveContract.ScreenshotsPrefix + "dir.png", [1], 0x10)); break;
            case "unknown-path": entries.Add(new("credentials/key.dpapi", [1])); break;
            case "missing-manifest": entries.RemoveAll(e => e.Name == Manifest); break;
            case "bad-manifest": Replace(entries, Manifest, Encoding.UTF8.GetBytes("{\"format\":\"a\",\"format\":\"b\"}")); break;
            case "future-archive": SetManifest(entries, manifest with { ArchiveVersion = 999 }); break;
            case "future-schema": SetManifest(entries, manifest with { DatabaseSchema = manifest.DatabaseSchema with { AppliedMigrations = manifest.DatabaseSchema.AppliedMigrations.Add("20990101000000_Future") } }); break;
            case "older-schema": SetManifest(entries, manifest with { DatabaseSchema = manifest.DatabaseSchema with { AppliedMigrations = manifest.DatabaseSchema.AppliedMigrations.RemoveAt(manifest.DatabaseSchema.AppliedMigrations.Length - 1) } }); break;
            case "declared-size": SetManifest(entries, manifest with { Files = manifest.Files.Select(p => p.Path == Shot ? p with { SizeBytes = 2 } : p).ToImmutableArray() }); break;
            case "invalid-db": Replace(entries, Db, new byte[128]); Rehash(entries, Db); break;
            case "wrong-preferences": entries.Add(new(BackupArchiveContract.PreferencesPath, Encoding.UTF8.GetBytes("{\"secret\":\"synthetic\"}"))); Rehash(entries, BackupArchiveContract.PreferencesPath, BackupFileKind.Preferences); break;
        }
        Write(f.Output, entries); string installed = InstalledHash(f);
        var result = await new RestorePreflightService().InspectAsync(f.Output, f.Root);
        Assert.Equal(expected, result.Failure); Assert.False(result.Validated); Assert.Null(result.Summary); Assert.False(result.CleanupFailed);
        if (defect == "older-schema") Assert.Equal(BackupSchemaCompatibility.RequiresStagedMigration, result.Compatibility);
        Assert.Equal(installed, InstalledHash(f)); Assert.Equal(new byte[] { 1, 2, 3 }, await File.ReadAllBytesAsync(Path.Combine(f.Paths.ScreenshotsDirectory, "image.png")));
        Assert.DoesNotContain(f.Root, result.ToString()); Clean(f);
    }

    [Theory]
    [InlineData("fk", BackupValidationCode.DatabaseIntegrityFailed)]
    [InlineData("schema", BackupValidationCode.UnsupportedDatabaseSchema)]
    [InlineData("migration", BackupValidationCode.UnsupportedDatabaseSchema)]
    [InlineData("reference", BackupValidationCode.DatabaseAttachmentMismatch)]
    [InlineData("unsafe-reference", BackupValidationCode.DatabaseAttachmentMismatch)]
    public async Task RehashedDatabaseStillRequiresIntegritySchemaAndExactAttachments(string defect, BackupValidationCode expected)
    {
        await using var f = await Fixture.Create(); await f.Add("image.png"); Assert.True((await f.Service().CreateAsync(f.Output)).Succeeded);
        var entries = Read(f.Output); string temporary = Path.Combine(f.Root, "mutated.db");
        await File.WriteAllBytesAsync(temporary, entries.Single(e => e.Name == Db).Bytes);
        using (var db = new SqliteConnection($"Data Source={temporary};Pooling=False"))
        {
            db.Open(); using var command = db.CreateCommand(); command.CommandText = defect switch
            {
                "fk" => "PRAGMA foreign_keys=OFF; UPDATE TradeScreenshots SET TradeId='missing';",
                "schema" => "DROP TABLE CoachingAnalyses;",
                "migration" => "DELETE FROM __EFMigrationsHistory WHERE MigrationId=(SELECT MAX(MigrationId) FROM __EFMigrationsHistory);",
                "unsafe-reference" => "UPDATE TradeScreenshots SET StorageKey='../escape.png';",
                _ => "UPDATE TradeScreenshots SET StorageKey='missing.png';"
            }; command.ExecuteNonQuery();
        }
        Replace(entries, Db, await File.ReadAllBytesAsync(temporary)); File.Delete(temporary); Rehash(entries, Db); Write(f.Output, entries);
        string installed = InstalledHash(f);
        var result = await new RestorePreflightService().InspectAsync(f.Output, f.Root);
        Assert.Equal(expected, result.Failure); Assert.Null(result.Summary); Clean(f); Assert.Equal(installed, InstalledHash(f));
    }

    [Theory]
    [InlineData(RestorePreflightPhase.Container)]
    [InlineData(RestorePreflightPhase.Manifest)]
    [InlineData(RestorePreflightPhase.Staging)]
    [InlineData(RestorePreflightPhase.Payloads)]
    [InlineData(RestorePreflightPhase.Database)]
    [InlineData(RestorePreflightPhase.Attachments)]
    [InlineData(RestorePreflightPhase.Summary)]
    public async Task CancellationAtEveryBoundaryCleansAndDoesNotPublish(RestorePreflightPhase cancelAt)
    {
        await using var f = await Fixture.Create(); Assert.True((await f.Service().CreateAsync(f.Output)).Succeeded);
        using var cts = new CancellationTokenSource(); string before = InstalledHash(f);
        var service = new RestorePreflightService((phase, _) => { if (phase == cancelAt) cts.Cancel(); }, new());
        var result = await service.InspectAsync(f.Output, f.Root, cts.Token);
        Assert.Equal(BackupValidationCode.Cancelled, result.Failure); Assert.Null(result.Summary); Assert.False(result.CleanupFailed);
        Clean(f); Assert.Equal(before, InstalledHash(f));
    }

    [Theory]
    [InlineData("files")]
    [InlineData("archive")]
    [InlineData("database")]
    [InlineData("manifest")]
    [InlineData("payload")]
    [InlineData("screenshot")]
    [InlineData("sqlite-work")]
    public async Task LimitsApplyBeforeUnboundedAllocationOrDatabaseWork(string limit)
    {
        await using var f = await Fixture.Create(); await f.Add("image.png"); Assert.True((await f.Service().CreateAsync(f.Output)).Succeeded);
        var limits = limit switch
        {
            "files" => new PreflightLimits(Files: 1), "archive" => new(ArchiveBytes: 22), "database" => new(DatabaseBytes: 1),
            "manifest" => new(ManifestBytes: 1), "payload" => new(PayloadBytes: 1), "screenshot" => new(ScreenshotBytes: 1),
            _ => new(DatabaseProgressCallbacks: 0, DatabaseProgressInterval: 1)
        };
        string before = InstalledHash(f); var result = await new RestorePreflightService(null, limits).InspectAsync(f.Output, f.Root);
        Assert.Equal(BackupValidationCode.LimitExceeded, result.Failure); Assert.Null(result.Summary); Clean(f); Assert.Equal(before, InstalledHash(f));
    }

    [Fact]
    public async Task CleanupFailureIsExplicitAndDoesNotDeleteUnownedFiles()
    {
        await using var f = await Fixture.Create(); Assert.True((await f.Service().CreateAsync(f.Output)).Succeeded);
        string? foreign = null;
        var service = new RestorePreflightService((phase, path) =>
        {
            if (phase != RestorePreflightPhase.Database) return;
            foreign = Path.Combine(path!, "not-owned"); File.WriteAllText(foreign, "synthetic"); throw new IOException("sensitive-path-never-returned");
        }, new());
        var result = await service.InspectAsync(f.Output, f.Root);
        Assert.Equal(BackupValidationCode.IoFailure, result.Failure); Assert.True(result.CleanupFailed);
        Assert.True(File.Exists(foreign)); Assert.DoesNotContain("sensitive", result.ToString());
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(foreign!)!));
    }

    [Fact]
    public async Task SourceIsPinnedDuringInspectionAndResultDoesNotAuthorizeLaterChangedArchive()
    {
        await using var f = await Fixture.Create(); Assert.True((await f.Service().CreateAsync(f.Output)).Succeeded);
        var service = new RestorePreflightService((phase, _) =>
        {
            if (phase == RestorePreflightPhase.Manifest) Assert.Throws<IOException>(() => File.WriteAllBytes(f.Output, [1]));
        }, new());
        var first = await service.InspectAsync(f.Output, f.Root); Assert.True(first.Validated); Clean(f);
        var entries = Read(f.Output); SetManifest(entries, Parse(entries) with { CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1) }); Write(f.Output, entries);
        var second = await service.InspectAsync(f.Output, f.Root); Assert.True(second.Validated);
        Assert.NotEqual(first.Summary!.ArchiveSha256, second.Summary!.ArchiveSha256); Clean(f);
    }

    [Theory]
    [InlineData("truncated", BackupValidationCode.IncompleteArchive)]
    [InlineData("local-name", BackupValidationCode.IncompleteArchive)]
    [InlineData("encrypted", BackupValidationCode.IncompleteArchive)]
    [InlineData("unsupported-compression", BackupValidationCode.IncompleteArchive)]
    [InlineData("crc", BackupValidationCode.ContentMismatch)]
    [InlineData("bomb", BackupValidationCode.LimitExceeded)]
    [InlineData("bomb-valid-prefix", BackupValidationCode.LimitExceeded)]
    [InlineData("count", BackupValidationCode.LimitExceeded)]
    [InlineData("overlap", BackupValidationCode.IncompleteArchive)]
    public async Task ContainerForgeryIsBoundedAndNeverReachesInstalledData(string defect, BackupValidationCode expected)
    {
        await using var f = await Fixture.Create(); await f.Add("image.png"); Assert.True((await f.Service().CreateAsync(f.Output)).Succeeded);
        var entries = Read(f.Output);
        if (defect is "bomb" or "bomb-valid-prefix")
        {
            Replace(entries, Shot, new byte[1024 * 1024]); Rehash(entries, Shot);
            var manifest = Parse(entries);
            SetManifest(entries, manifest with { Files = manifest.Files.Select(p => p.Path == Shot ? p with { SizeBytes = 3,
                Sha256 = defect == "bomb-valid-prefix" ? Convert.ToHexStringLower(SHA256.HashData(new byte[3])) : p.Sha256 } : p).ToImmutableArray() });
            Write(f.Output, entries);
        }
        var bytes = await File.ReadAllBytesAsync(f.Output); var (central, local) = Offsets(bytes, Shot); int end = bytes.Length - 22;
        switch (defect)
        {
            case "truncated": bytes = bytes[..^1]; break;
            case "local-name": bytes[local + 30] ^= 1; break;
            case "encrypted": Put16(bytes, local + 6, 1); Put16(bytes, central + 8, 1); break;
            case "unsupported-compression": Put16(bytes, local + 8, 12); Put16(bytes, central + 10, 12); break;
            case "crc": Put32(bytes, local + 14, 123); Put32(bytes, central + 16, 123); break;
            case "bomb": Put32(bytes, local + 22, 3); Put32(bytes, central + 24, 3); break;
            case "bomb-valid-prefix":
                Put32(bytes, local + 22, 3); Put32(bytes, central + 24, 3);
                uint crc = uint.MaxValue;
                for (int i = 0; i < 24; i++) crc = (crc & 1) != 0 ? 0xedb88320 ^ (crc >> 1) : crc >> 1;
                Put32(bytes, local + 14, ~crc); Put32(bytes, central + 16, ~crc); break;
            case "count": Put16(bytes, end + 8, 50000); Put16(bytes, end + 10, 50000); break;
            case "overlap": Put32(bytes, central + 42, (uint)Offsets(bytes, Db).Local); break;
        }
        await File.WriteAllBytesAsync(f.Output, bytes); string installed = InstalledHash(f);
        var result = await new RestorePreflightService(null, new(Files: defect == "count" ? 5 : BackupArchiveContract.MaximumFiles)).InspectAsync(f.Output, f.Root);
        Assert.Equal(expected, result.Failure); Assert.Null(result.Summary); Assert.False(result.CleanupFailed); Clean(f);
        Assert.Equal(installed, InstalledHash(f));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ValidDataDescriptorsAndZip64DirectoryRemainCompatible(bool zip64)
    {
        await using var f = await Fixture.Create(); await f.Add("image.png"); Assert.True((await f.Service().CreateAsync(f.Output)).Succeeded);
        if (!zip64)
        {
            var entries = Read(f.Output);
            using var bytes = new MemoryStream();
            using (var nonseekable = new Nonseekable(bytes))
            using (var zip = new ZipArchive(nonseekable, ZipArchiveMode.Create, leaveOpen: true))
                foreach (var payload in entries)
                { var entry = zip.CreateEntry(payload.Name); entry.ExternalAttributes = 0; using var stream = entry.Open(); stream.Write(payload.Bytes); }
            await File.WriteAllBytesAsync(f.Output, bytes.ToArray());
        }
        else
        {
            var bytes = await File.ReadAllBytesAsync(f.Output); int end = bytes.Length - 22;
            var expanded = new byte[bytes.Length + 76]; Array.Copy(bytes, expanded, end);
            Put32(expanded, end, 0x06064b50); Put64(expanded, end + 4, 44); Put16(expanded, end + 12, 45); Put16(expanded, end + 14, 45);
            Put64(expanded, end + 24, U16(bytes, end + 10)); Put64(expanded, end + 32, U16(bytes, end + 10));
            Put64(expanded, end + 40, U32(bytes, end + 12)); Put64(expanded, end + 48, U32(bytes, end + 16));
            Put32(expanded, end + 56, 0x07064b50); Put64(expanded, end + 64, (ulong)end); Put32(expanded, end + 72, 1);
            Array.Copy(bytes, end, expanded, end + 76, 22);
            Put16(expanded, end + 84, ushort.MaxValue); Put16(expanded, end + 86, ushort.MaxValue);
            Put32(expanded, end + 88, uint.MaxValue); Put32(expanded, end + 92, uint.MaxValue);
            await File.WriteAllBytesAsync(f.Output, expanded);
        }
        var result = await new RestorePreflightService().InspectAsync(f.Output, f.Root); Assert.True(result.Validated, result.ToString()); Clean(f);
    }

    private static (int Central, int Local) Offsets(byte[] bytes, string name)
    {
        int central = (int)U32(bytes, bytes.Length - 22 + 16);
        while (U32(bytes, central) == 0x02014b50)
        {
            int length = U16(bytes, central + 28);
            if (Encoding.ASCII.GetString(bytes, central + 46, length) == name) return (central, (int)U32(bytes, central + 42));
            central += 46 + length + U16(bytes, central + 30) + U16(bytes, central + 32);
        }
        throw new InvalidOperationException("Synthetic entry not found.");
    }
    private static ushort U16(byte[] bytes, int at) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(at));
    private static uint U32(byte[] bytes, int at) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(at));
    private static void Put16(byte[] bytes, int at, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(at), value);
    private static void Put32(byte[] bytes, int at, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at), value);
    private static void Put64(byte[] bytes, int at, ulong value) => BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(at), value);
    private sealed class Nonseekable(Stream output) : Stream
    {
        public override bool CanRead => false; public override bool CanSeek => false; public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException(); public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => output.Flush(); public override void Write(byte[] buffer, int offset, int count) => output.Write(buffer, offset, count);
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException(); public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }

    private sealed record Payload(string Name, byte[] Bytes, int Attributes = 0);
    private static List<Payload> Read(string path)
    {
        using var zip = ZipFile.OpenRead(path); return zip.Entries.Select(e =>
        { using var stream = e.Open(); using var bytes = new MemoryStream(); stream.CopyTo(bytes); return new Payload(e.FullName, bytes.ToArray(), e.ExternalAttributes); }).ToList();
    }
    private static void Write(string path, List<Payload> entries)
    {
        using var file = File.Create(path); using var zip = new ZipArchive(file, ZipArchiveMode.Create);
        foreach (var payload in entries) { var entry = zip.CreateEntry(payload.Name, CompressionLevel.Fastest); entry.ExternalAttributes = payload.Attributes;
            using var stream = entry.Open(); stream.Write(payload.Bytes); }
    }
    private static BackupManifest Parse(List<Payload> entries) => JsonSerializer.Deserialize<BackupManifest>(entries.Single(e => e.Name == Manifest).Bytes, BackupArchiveContract.JsonOptions)!;
    private static void SetManifest(List<Payload> entries, BackupManifest manifest) => Replace(entries, Manifest, JsonSerializer.SerializeToUtf8Bytes(manifest, BackupArchiveContract.JsonOptions));
    private static void Replace(List<Payload> entries, string name, byte[] bytes) { int index = entries.FindIndex(e => e.Name == name); entries[index] = entries[index] with { Bytes = bytes }; }
    private static void Rehash(List<Payload> entries, string name, BackupFileKind? add = null)
    {
        var manifest = Parse(entries); var bytes = entries.Single(e => e.Name == name).Bytes;
        var item = new BackupFileEntry(name, add ?? manifest.Files.Single(p => p.Path == name).Kind, bytes.Length, Convert.ToHexStringLower(SHA256.HashData(bytes)));
        SetManifest(entries, manifest with { Files = add.HasValue ? manifest.Files.Add(item) : manifest.Files.Select(p => p.Path == name ? item : p).ToImmutableArray() });
    }
    private static string InstalledHash(Fixture f)
    {
        using var db = new SqliteConnection($"Data Source={f.Paths.DatabasePath};Pooling=False"); db.Open();
        using var command = db.CreateCommand(); command.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);"; command.ExecuteNonQuery();
        db.Close();
        Persistence.SqliteTestPoolCleanup.ClearPersistencePools(f.Paths.DatabasePath);
        return Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(f.Paths.DatabasePath)));
    }
    private static void Clean(Fixture f) => Assert.Empty(Directory.GetDirectories(f.Root, ".preflight-*.partial"));
}
