using System.Collections.Immutable;
using System.Text.Json;
using PersonalTradingJournal.Application.Backups;

namespace PersonalTradingJournal.Application.Tests.Backups;

public sealed class BackupManifestTests
{
    private static readonly ImmutableArray<string> Migrations = ["20260101000000_Initial", "20260102000000_History"];
    private static readonly string Hash = new('a', 64);
    private static BackupFileEntry Database => new(BackupArchiveContract.DatabasePath, BackupFileKind.Database, 4096, Hash);
    private static BackupFileEntry Screenshot => new(BackupArchiveContract.ScreenshotsPrefix + "0123456789abcdef.png", BackupFileKind.Screenshot, 128, Hash);
    private static BackupManifest Valid => new(BackupArchiveContract.Format, 1, "1.0.0",
        new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero), new("sqlite", Migrations), "SHA-256", [Database, Screenshot]);
    private static BackupManifestCheck Check(BackupManifest? manifest) => BackupManifestValidator.Validate(manifest, Migrations);
    private static void Has(BackupManifest? manifest, BackupValidationCode code) => Assert.Contains(Check(manifest).Issues, i => i.Code == code);

    [Fact]
    public void ImmutableManifestRoundTripsWithPinnedWireNamesAndNoLocalIdentity()
    {
        string json = JsonSerializer.Serialize(Valid, BackupArchiveContract.JsonOptions);
        var copy = JsonSerializer.Deserialize<BackupManifest>(json, BackupArchiveContract.JsonOptions)!;
        Assert.True(Check(copy).IsValidManifest);
        Assert.Equal(BackupSchemaCompatibility.Exact, Check(copy).SchemaCompatibility);
        Assert.Equal(Valid.Files.ToArray(), copy.Files.ToArray());
        Assert.Equal(Valid.CreatedAtUtc, copy.CreatedAtUtc);
        Assert.Equal(json, JsonSerializer.Serialize(copy, BackupArchiveContract.JsonOptions));
        Assert.Contains("\"archiveVersion\":1", json);
        Assert.Contains("\"kind\":\"Screenshot\"", json);
        Assert.DoesNotContain("credential", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("account", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("C:", json, StringComparison.OrdinalIgnoreCase);
        Assert.True(BackupArchiveContract.JsonOptions.IsReadOnly);
    }

    [Fact]
    public void StrictWireContractRejectsMissingUnknownAndNullPropertiesAndNumericKinds()
    {
        string json = JsonSerializer.Serialize(Valid, BackupArchiveContract.JsonOptions);
        foreach (string invalid in new[]
        {
            json.Replace("\"archiveVersion\":1,", ""),
            json.Insert(1, "\"apiKey\":\"synthetic-not-a-key\","),
            json.Insert(1, "\"archiveVersion\":1,"),
            json.Replace("\"applicationVersion\":\"1.0.0\"", "\"applicationVersion\":null"),
            json.Replace("\"kind\":\"Database\"", "\"kind\":0")
        }) Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<BackupManifest>(invalid, BackupArchiveContract.JsonOptions));
    }

    [Fact]
    public void DatabaseOnlySupportsAnEmptyJournalButDatabaseIsNeverOptional()
    {
        Assert.True(Check(Valid with { Files = [Database] }).IsValidManifest);
        Assert.True(Check(Valid with { Files = [Database, new(BackupArchiveContract.PreferencesPath, BackupFileKind.Preferences, 32, Hash)] }).IsValidManifest);
        Has(Valid with { Files = [] }, BackupValidationCode.MissingEntry);
        Has(Valid with { Files = [Screenshot] }, BackupValidationCode.MissingEntry);
        Has(Valid with { Files = default }, BackupValidationCode.InvalidManifest);
        Has(null, BackupValidationCode.InvalidManifest);
        Has(Valid with { Files = [Database, null!] }, BackupValidationCode.InvalidManifest);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(int.MaxValue)]
    public void UnsupportedVersionDoesNotAttemptToInterpretItsInventory(int version) =>
        Assert.Equal(BackupValidationCode.UnsupportedArchiveVersion,
            Assert.Single(Check(Valid with { ArchiveVersion = version, Files = default }).Issues).Code);

    [Fact]
    public void SchemaCompatibilityUsesExactOrderedMigrationPrefixNotApplicationVersion()
    {
        var older = Check(Valid with { ApplicationVersion = "99.0.0", DatabaseSchema = new("sqlite", [Migrations[0]]) });
        Assert.True(older.IsValidManifest);
        Assert.Equal(BackupSchemaCompatibility.RequiresStagedMigration, older.SchemaCompatibility);
        foreach (var schema in new BackupDatabaseSchema[]
        {
            new("sqlite", []), new("other", Migrations), new("sqlite", [Migrations[1]]),
            new("sqlite", [Migrations[0], Migrations[0]]), new("sqlite", [Migrations[1], Migrations[0]]),
            new("sqlite", [.. Migrations, "20260103000000_Future"]), new("sqlite", default), null!
        }) Has(Valid with { DatabaseSchema = schema }, BackupValidationCode.UnsupportedDatabaseSchema);
    }

    [Fact]
    public void UtcHashAndApplicationVersionHaveNoPermissiveFallback()
    {
        Has(Valid with { CreatedAtUtc = default }, BackupValidationCode.InvalidUtcTimestamp);
        Has(Valid with { CreatedAtUtc = Valid.CreatedAtUtc.ToOffset(TimeSpan.FromHours(3)) }, BackupValidationCode.InvalidUtcTimestamp);
        Has(Valid with { Format = "other" }, BackupValidationCode.UnsupportedArchiveVersion);
        Has(Valid with { HashAlgorithm = "MD5" }, BackupValidationCode.InvalidManifest);
        foreach (string version in new[] { "1", "1.0", "C:/local", "1.0.0 secret", "1.0.-1", "" })
            Has(Valid with { ApplicationVersion = version }, BackupValidationCode.InvalidManifest);
        foreach (string hash in new[] { "", new string('a', 63), new string('A', 64), new string('g', 64) })
            Has(Valid with { Files = [Database with { Sha256 = hash }] }, BackupValidationCode.InvalidHash);
    }

    [Theory]
    [InlineData("/data/journal.db")]
    [InlineData("C:/journal.db")]
    [InlineData("\\\\server\\share\\journal.db")]
    [InlineData("data\\journal.db")]
    [InlineData("../data/journal.db")]
    [InlineData("attachments/screenshots/../image.png")]
    [InlineData("attachments/screenshots/image.png:secret")]
    [InlineData("attachments/screenshots/image.png.")]
    [InlineData("attachments/screenshots/image.png ")]
    [InlineData("attachments/screenshots/CON.png")]
    [InlineData("attachments/screenshots/lpt1.jpg")]
    [InlineData("attachments/screenshots/COM9.webp")]
    [InlineData("attachments/screenshots/%2e%2e.png")]
    [InlineData("attachments//screenshots/image.png")]
    [InlineData("attachments/screenshots/é.png")]
    [InlineData("attachments/screenshots/a\u0000.png")]
    public void UnsafePathsAreRejectedWithoutOperatingSystemNormalization(string path) =>
        Has(Valid with { Files = [Database, Screenshot with { Path = path }] }, BackupValidationCode.UnsafePath);

    [Theory]
    [InlineData("manifest.json")]
    [InlineData("data/journal.db-wal")]
    [InlineData("logs/log.txt")]
    [InlineData("backups/old.zip")]
    [InlineData("PersonalTradingJournal.Secrets/groq.dpapi")]
    [InlineData("attachments/screenshots/image.tmp")]
    [InlineData("attachments/screenshots/sub/image.png")]
    public void UndeclaredRolesAndExcludedFilesCannotEnterPortableInventory(string path) =>
        Has(Valid with { Files = [Database, Screenshot with { Path = path }] }, BackupValidationCode.UnexpectedEntry);

    [Fact]
    public void DuplicateEntriesIncludingWindowsCaseAliasesAreRejected()
    {
        Has(Valid with { Files = [Database, Database] }, BackupValidationCode.DuplicateEntry);
        Has(Valid with { Files = [Database, Screenshot, Screenshot with { Path = Screenshot.Path.ToUpperInvariant() }] }, BackupValidationCode.DuplicateEntry);
        Has(Valid with { Files = [Database with { Kind = (BackupFileKind)99 }] }, BackupValidationCode.UnexpectedEntry);
    }

    [Theory]
    [InlineData(BackupFileKind.Database, BackupArchiveContract.MaximumDatabaseBytes)]
    [InlineData(BackupFileKind.Screenshot, BackupArchiveContract.MaximumScreenshotBytes)]
    [InlineData(BackupFileKind.Preferences, BackupArchiveContract.MaximumPreferencesBytes)]
    public void PerRoleSizeLimitsRejectZeroNegativeAndOverflow(BackupFileKind kind, long maximum)
    {
        var file = kind switch
        {
            BackupFileKind.Database => Database,
            BackupFileKind.Screenshot => Screenshot,
            _ => new(BackupArchiveContract.PreferencesPath, kind, 1, Hash)
        };
        BackupManifest WithSize(long size) => Valid with { Files = kind == BackupFileKind.Database
            ? [file with { SizeBytes = size }] : [Database, file with { SizeBytes = size }] };
        Assert.True(Check(WithSize(maximum)).IsValidManifest);
        foreach (long size in new[] { 0, -1, maximum + 1, long.MaxValue }) Has(WithSize(size), BackupValidationCode.LimitExceeded);
    }

    [Fact]
    public void AggregateLimitsBoundPathsPayloadCountAndDiagnosticVolume()
    {
        string prefix = BackupArchiveContract.ScreenshotsPrefix;
        var boundary = Screenshot with { Path = prefix + new string('a', BackupArchiveContract.MaximumPathCharacters - prefix.Length - 4) + ".png" };
        Assert.True(Check(Valid with { Files = [Database, boundary] }).IsValidManifest);
        Has(Valid with { Files = [Database, boundary with { Path = boundary.Path + "a" }] }, BackupValidationCode.UnsafePath);
        var many = Enumerable.Range(0, 64).Select(i => Screenshot with { Path = prefix + i + ".png", SizeBytes = BackupArchiveContract.MaximumScreenshotBytes });
        Has(Valid with { Files = [Database, .. many] }, BackupValidationCode.LimitExceeded);
        Has(Valid with { Files = Enumerable.Repeat(Database, BackupArchiveContract.MaximumFiles + 1).ToImmutableArray() }, BackupValidationCode.LimitExceeded);
        Assert.Equal(BackupArchiveContract.MaximumReportedIssues,
            Check(Valid with { Files = Enumerable.Repeat(Database, 100).ToImmutableArray() }).Issues.Length);
    }

    [Fact]
    public void CancellationAndInvalidTrustedCatalogCannotStartAnyWork()
    {
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Assert.Throws<OperationCanceledException>(() => BackupManifestValidator.Validate(Valid, Migrations, cancelled.Token));
        Assert.Throws<ArgumentException>(() => BackupManifestValidator.Validate(Valid, []));
        Assert.Throws<ArgumentException>(() => BackupManifestValidator.Validate(Valid, [Migrations[0], Migrations[0]]));
        Assert.Throws<ArgumentException>(() => BackupManifestValidator.Validate(Valid, [Migrations[1], Migrations[0]]));
        Assert.Throws<ArgumentException>(() => BackupManifestValidator.Validate(Valid, ["local/path"]));
    }
}
