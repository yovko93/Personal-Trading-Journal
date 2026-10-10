using System.Collections.Immutable;

namespace PersonalTradingJournal.Application.Backups;

/// <summary>Pure bounded contract checks; never reads files, opens SQLite, extracts ZIPs or authorizes replacement.</summary>
public static class BackupManifestValidator
{
    public static BackupManifestCheck Validate(BackupManifest? manifest,
        ImmutableArray<string> knownMigrations, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (knownMigrations.IsDefaultOrEmpty || knownMigrations.Length > BackupArchiveContract.MaximumMigrations ||
            knownMigrations.Any(m => !MigrationId(m)) || knownMigrations.Distinct(StringComparer.Ordinal).Count() != knownMigrations.Length ||
            !knownMigrations.SequenceEqual(knownMigrations.Order(StringComparer.Ordinal), StringComparer.Ordinal))
            throw new ArgumentException("A trusted ordered migration catalog is required.", nameof(knownMigrations));

        var issues = ImmutableArray.CreateBuilder<BackupValidationIssue>();
        void Add(BackupValidationCode code, int? index = null)
        {
            if (issues.Count < BackupArchiveContract.MaximumReportedIssues) issues.Add(new(code, index));
        }
        var compatibility = BackupSchemaCompatibility.Unsupported;
        BackupManifestCheck Result() => new(compatibility, issues.ToImmutable());
        if (manifest is null) { Add(BackupValidationCode.InvalidManifest); return Result(); }
        if (manifest.Format != BackupArchiveContract.Format || manifest.ArchiveVersion != BackupArchiveContract.Version)
        { Add(BackupValidationCode.UnsupportedArchiveVersion); return Result(); }
        if (!ProductVersion(manifest.ApplicationVersion) || manifest.HashAlgorithm != BackupArchiveContract.HashAlgorithm)
            Add(BackupValidationCode.InvalidManifest);
        if (manifest.CreatedAtUtc == default || manifest.CreatedAtUtc.Offset != TimeSpan.Zero)
            Add(BackupValidationCode.InvalidUtcTimestamp);
        var schema = manifest.DatabaseSchema;
        if (schema is null || schema.Engine != "sqlite" || schema.AppliedMigrations.IsDefaultOrEmpty ||
            schema.AppliedMigrations.Length > knownMigrations.Length ||
            !schema.AppliedMigrations.SequenceEqual(knownMigrations.Take(schema.AppliedMigrations.Length), StringComparer.Ordinal))
            Add(BackupValidationCode.UnsupportedDatabaseSchema);
        else compatibility = schema.AppliedMigrations.Length == knownMigrations.Length
            ? BackupSchemaCompatibility.Exact : BackupSchemaCompatibility.RequiresStagedMigration;

        if (manifest.Files.IsDefault) { Add(BackupValidationCode.InvalidManifest); return Result(); }
        if (manifest.Files.Length > BackupArchiveContract.MaximumFiles)
        { Add(BackupValidationCode.LimitExceeded); return Result(); }
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int databases = 0, screenshots = 0;
        long total = 0;
        for (int i = 0; i < manifest.Files.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var file = manifest.Files[i];
            if (file is null) { Add(BackupValidationCode.InvalidManifest, i); continue; }
            if (!SafePath(file.Path)) Add(BackupValidationCode.UnsafePath, i);
            else
            {
                if (!paths.Add(file.Path)) Add(BackupValidationCode.DuplicateEntry, i);
                if (!AllowedPath(file)) Add(BackupValidationCode.UnexpectedEntry, i);
            }
            if (file.Kind == BackupFileKind.Database && file.Path == BackupArchiveContract.DatabasePath) databases++;
            if (file.Kind == BackupFileKind.Screenshot) screenshots++;
            long maximum = file.Kind switch
            {
                BackupFileKind.Database => BackupArchiveContract.MaximumDatabaseBytes,
                BackupFileKind.Screenshot => BackupArchiveContract.MaximumScreenshotBytes,
                BackupFileKind.Preferences => BackupArchiveContract.MaximumPreferencesBytes,
                _ => 0
            };
            if (file.SizeBytes <= 0 || file.SizeBytes > maximum) Add(BackupValidationCode.LimitExceeded, i);
            // Subtraction before addition avoids overflow even for hostile declarations.
            if (file.SizeBytes > BackupArchiveContract.MaximumPayloadBytes - total)
                Add(BackupValidationCode.LimitExceeded, i);
            else if (file.SizeBytes > 0) total += file.SizeBytes;
            if (file.Sha256 is not { Length: 64 } || !file.Sha256.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f'))
                Add(BackupValidationCode.InvalidHash, i);
        }
        if (databases == 0) Add(BackupValidationCode.MissingEntry);
        if (databases > 1) Add(BackupValidationCode.DuplicateEntry);
        if (screenshots > BackupArchiveContract.MaximumFiles - 2) Add(BackupValidationCode.LimitExceeded);
        return Result();
    }

    private static bool ProductVersion(string? value) => value is { Length: <= 23 } &&
        value.Split('.').Length is 3 or 4 && value.All(c => char.IsAsciiDigit(c) || c == '.') &&
        System.Version.TryParse(value, out _);

    private static bool MigrationId(string? value) => value is { Length: >= 16 and <= 160 } &&
        value.Take(14).All(char.IsAsciiDigit) && value[14] == '_' &&
        value.Skip(15).All(c => char.IsAsciiLetterOrDigit(c) || c == '_');

    private static bool SafePath(string? path)
    {
        if (path is not { Length: > 0 } || path.Length > BackupArchiveContract.MaximumPathCharacters) return false;
        foreach (string part in path.Split('/'))
        {
            if (part.Length == 0 || part[0] == '.' || part[^1] == '.' ||
                !part.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.')) return false;
            string stem = part.Split('.')[0].ToUpperInvariant();
            if (stem is "CON" or "PRN" or "AUX" or "NUL" ||
                stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal)) &&
                stem[3] is >= '1' and <= '9') return false;
        }
        return true;
    }

    private static bool AllowedPath(BackupFileEntry file) => file.Kind switch
    {
        BackupFileKind.Database => file.Path == BackupArchiveContract.DatabasePath,
        BackupFileKind.Preferences => file.Path == BackupArchiveContract.PreferencesPath,
        BackupFileKind.Screenshot => file.Path.StartsWith(BackupArchiveContract.ScreenshotsPrefix, StringComparison.Ordinal) &&
            !file.Path[BackupArchiveContract.ScreenshotsPrefix.Length..].Contains('/') &&
            new[] { ".png", ".jpg", ".jpeg", ".webp" }.Any(extension => file.Path.EndsWith(extension, StringComparison.Ordinal)),
        _ => false
    };

    public static bool IsPortableScreenshotKey(string? key) => key is not null &&
        SafePath(BackupArchiveContract.ScreenshotsPrefix + key) &&
        AllowedPath(new(BackupArchiveContract.ScreenshotsPrefix + key, BackupFileKind.Screenshot, 1, ""));

    public static bool IsSafeRelativePath(string? path) => SafePath(path);
}
