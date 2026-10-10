using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PersonalTradingJournal.Application.Backups;

/// <summary>Portable, unencrypted ZIP contract. No archive or filesystem operations are implemented here.</summary>
public static class BackupArchiveContract
{
    public const string Format = "personal-trading-journal-backup";
    public const int Version = 1;
    public const string ManifestPath = "manifest.json";
    public const string DatabasePath = "data/journal.db";
    public const string PreferencesPath = "preferences/settings.json";
    public const string ScreenshotsPrefix = "attachments/screenshots/";
    public const string HashAlgorithm = "SHA-256";
    public const int MaximumFiles = 100_002; // Database, optional preferences, 100,000 distinct screenshot keys.
    public const int MaximumPathCharacters = 240;
    public const int MaximumManifestBytes = 32 * 1024 * 1024;
    public const long MaximumDatabaseBytes = 8L * 1024 * 1024 * 1024;
    public const long MaximumScreenshotBytes = 512L * 1024 * 1024;
    public const long MaximumPreferencesBytes = 64 * 1024;
    public const long MaximumPayloadBytes = 32L * 1024 * 1024 * 1024;
    public const long MaximumArchiveBytes = 34L * 1024 * 1024 * 1024;
    public const int MaximumMigrations = 1024;
    public const int MaximumReportedIssues = 64;

    public static JsonSerializerOptions JsonOptions { get; } = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            RespectNullableAnnotations = true,
            RespectRequiredConstructorParameters = true,
            AllowDuplicateProperties = false,
            MaxDepth = 16
        };
        options.Converters.Add(new JsonStringEnumConverter<BackupFileKind>(allowIntegerValues: false));
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}

// Serialized as camelCase properties and string enums; no local paths, user labels or credentials.
public sealed record BackupManifest(string Format, int ArchiveVersion, string ApplicationVersion,
    DateTimeOffset CreatedAtUtc, BackupDatabaseSchema DatabaseSchema, string HashAlgorithm,
    ImmutableArray<BackupFileEntry> Files);

/// <summary>Ordered EF migration IDs, compared with the reader's trusted migration catalog, not app version strings.</summary>
public sealed record BackupDatabaseSchema(string Engine, ImmutableArray<string> AppliedMigrations);

/// <summary>Size and lowercase SHA-256 of the complete uncompressed bytes. Manifest is not self-listed.</summary>
public sealed record BackupFileEntry(string Path, BackupFileKind Kind, long SizeBytes, string Sha256);

public enum BackupFileKind { Database, Screenshot, Preferences }
public enum BackupSchemaCompatibility { Exact, RequiresStagedMigration, Unsupported }

/// <summary>Shared safe codes for later transport/payload/database checks as well as today's metadata checks.</summary>
public enum BackupValidationCode
{
    UnsupportedArchiveVersion, InvalidManifest, InvalidUtcTimestamp, UnsupportedDatabaseSchema,
    MissingEntry, DuplicateEntry, UnexpectedEntry, UnsafePath, LimitExceeded, InvalidHash,
    ContentMismatch, IncompleteArchive, DatabaseIntegrityFailed, DatabaseAttachmentMismatch,
    SourceChanged, IoFailure, Cancelled
}

/// <summary>Ordinal only: do not echo an untrusted archive name, local path or raw exception into diagnostics.</summary>
public sealed record BackupValidationIssue(BackupValidationCode Code, int? FileIndex = null);

/// <summary>Declaration validation only. Even a valid manifest is NOT proof of a valid or restorable archive.</summary>
public sealed record BackupManifestCheck(BackupSchemaCompatibility SchemaCompatibility,
    ImmutableArray<BackupValidationIssue> Issues)
{
    public bool IsValidManifest => Issues.IsEmpty;
}
