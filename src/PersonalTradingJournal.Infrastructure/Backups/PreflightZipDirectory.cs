using System.Buffers.Binary;
using System.Text;
using PersonalTradingJournal.Application.Backups;

namespace PersonalTradingJournal.Infrastructure.Backups;

/// <summary>Bound central-directory parsing BEFORE ZipArchive allocates entry metadata. Single-volume v1 profile.</summary>
internal static class PreflightZipDirectory
{
    internal sealed record Entry(string Name, long Size, long CompressedSize, long Offset, uint Crc, ushort Flags, ushort Method, long DataOffset = 0);
    private const int MaximumMetadata = 64 * 1024 * 1024;
    internal static List<Entry> Read(FileStream file, PreflightLimits limits, CancellationToken token)
    {
        Require(file.Length >= 22);
        Limit(file.Length <= limits.ArchiveBytes);
        byte[] tail = ReadAt(file, Math.Max(0, file.Length - 65557), (int)Math.Min(65557, file.Length));
        int endIndex = -1;
        for (int i = tail.Length - 22; i >= 0; i--)
            if (U32(tail, i) == 0x06054b50 && i + 22 + U16(tail, i + 20) == tail.Length) { endIndex = i; break; }
        Require(endIndex >= 0);
        var end = tail.AsSpan(endIndex);
        Limit(U16(end, 20) <= 1024);
        Require(U16(end, 4) == 0 && U16(end, 6) == 0 && U16(end, 8) == U16(end, 10));
        long entries = U16(end, 10), size = U32(end, 12), offset = U32(end, 16);
        long endOffset = file.Length - tail.Length + endIndex, directoryEnd = endOffset;
        bool zip64 = endOffset >= 20 && U32(ReadAt(file, endOffset - 20, 4), 0) == 0x07064b50;
        if (zip64)
        {
            var locator = ReadAt(file, endOffset - 20, 20);
            Require(U32(locator, 4) == 0 && U32(locator, 16) == 1);
            long zip64Offset = Long(U64(locator, 8));
            var large = ReadAt(file, zip64Offset, 56);
            Require(U32(large, 0) == 0x06064b50 && U64(large, 4) == 44 && zip64Offset + 56 == endOffset - 20);
            Require(U32(large, 16) == 0 && U32(large, 20) == 0 && U64(large, 24) == U64(large, 32));
            long count64 = Long(U64(large, 32)), size64 = Long(U64(large, 40)), offset64 = Long(U64(large, 48));
            Require((entries == ushort.MaxValue || entries == count64) && (size == uint.MaxValue || size == size64) &&
                (offset == uint.MaxValue || offset == offset64));
            (entries, size, offset, directoryEnd) = (count64, size64, offset64, zip64Offset);
        }
        else Require(entries != ushort.MaxValue && size != uint.MaxValue && offset != uint.MaxValue);
        Limit(entries <= limits.Files + 1 && size <= MaximumMetadata);
        Require(entries >= 1 && offset >= 0 && size <= directoryEnd && offset == directoryEnd - size);
        file.Position = offset;
        var result = new List<Entry>(); var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long total = 0;
        for (long i = 0; i < entries; i++)
        {
            token.ThrowIfCancellationRequested();
            var header = ReadWithin(file, 46, directoryEnd);
            Require(U32(header, 0) == 0x02014b50 && U16(header, 6) <= 45);
            ushort flags = U16(header, 8), method = U16(header, 10);
            Require((flags & ~0x080e) == 0 && method is 0 or 8);
            uint attributes = U32(header, 38); uint type = (attributes >> 16) & 0xf000;
            if ((attributes & 0x418) != 0 || type is not (0 or 0x8000)) throw new PreflightFailure(BackupValidationCode.UnsafePath);
            int nameLength = U16(header, 28), extraLength = U16(header, 30), commentLength = U16(header, 32);
            Limit(nameLength <= BackupArchiveContract.MaximumPathCharacters && extraLength <= 1024 && commentLength <= 1024);
            byte[] rawName = ReadWithin(file, nameLength, directoryEnd);
            if (rawName.Any(b => b > 127)) throw new PreflightFailure(BackupValidationCode.UnsafePath);
            string name = Encoding.ASCII.GetString(rawName);
            if (!BackupManifestValidator.IsSafeRelativePath(name)) throw new PreflightFailure(BackupValidationCode.UnsafePath);
            if (!names.Add(name)) throw new PreflightFailure(BackupValidationCode.DuplicateEntry);
            long maximum = Maximum(name, limits);
            var extra = ReadWithin(file, extraLength, directoryEnd);
            long uncompressed = U32(header, 24), compressed = U32(header, 20), localOffset = U32(header, 42);
            long disk = U16(header, 34);
            ReadExtra(extra, ref uncompressed, ref compressed, ref localOffset, ref disk);
            Require(disk == 0 && compressed >= 0 && compressed <= offset && localOffset < offset);
            Limit(uncompressed > 0 && uncompressed <= maximum);
            if (name != BackupArchiveContract.ManifestPath)
            { Limit(uncompressed <= limits.PayloadBytes - total); total += uncompressed; }
            ReadWithin(file, commentLength, directoryEnd);
            result.Add(new(name, uncompressed, compressed, localOffset, U32(header, 16), flags, method));
        }
        Require(file.Position == directoryEnd);

        // No prefixes, overlaps, orphan local records or gaps. Local names/flags/sizes must agree with central records.
        long next = 0;
        foreach (var (entry, index) in result.Select((entry, index) => (entry, index)).OrderBy(e => e.entry.Offset))
        {
            token.ThrowIfCancellationRequested(); Require(entry.Offset == next);
            file.Position = entry.Offset;
            var header = ReadWithin(file, 30, offset);
            Require(U32(header, 0) == 0x04034b50 && U16(header, 4) <= 45 && U16(header, 6) == entry.Flags && U16(header, 8) == entry.Method);
            int nameLength = U16(header, 26), extraLength = U16(header, 28);
            Limit(nameLength <= BackupArchiveContract.MaximumPathCharacters && extraLength <= 1024);
            Require(ReadWithin(file, nameLength, offset).AsSpan().SequenceEqual(Encoding.ASCII.GetBytes(entry.Name)));
            var extra = ReadWithin(file, extraLength, offset);
            long uncompressed = U32(header, 22), compressed = U32(header, 18), unusedOffset = 0, unusedDisk = 0;
            bool largeSizes = uncompressed == uint.MaxValue || compressed == uint.MaxValue;
            ReadExtra(extra, ref uncompressed, ref compressed, ref unusedOffset, ref unusedDisk);
            Require(entry.CompressedSize <= offset - file.Position);
            result[index] = entry with { DataOffset = file.Position };
            file.Position += entry.CompressedSize;
            if ((entry.Flags & 8) == 0)
                Require(U32(header, 14) == entry.Crc && uncompressed == entry.Size && compressed == entry.CompressedSize);
            else
            {
                Require((U32(header, 14) == 0 || U32(header, 14) == entry.Crc) && (uncompressed == 0 || uncompressed == entry.Size) &&
                    (compressed == 0 || compressed == entry.CompressedSize));
                uint crc = U32(ReadWithin(file, 4, offset), 0);
                if (crc == 0x08074b50) crc = U32(ReadWithin(file, 4, offset), 0);
                var sizes = ReadWithin(file, largeSizes ? 16 : 8, offset);
                Require(crc == entry.Crc && (largeSizes ? Long(U64(sizes, 0)) : U32(sizes, 0)) == entry.CompressedSize &&
                    (largeSizes ? Long(U64(sizes, 8)) : U32(sizes, 4)) == entry.Size);
            }
            next = file.Position;
        }
        Require(next == offset);
        return result;
    }

    private static void ReadExtra(byte[] bytes, ref long size, ref long compressed, ref long offset, ref long disk)
    {
        bool found = false;
        bool requiresZip64 = size == uint.MaxValue || compressed == uint.MaxValue || offset == uint.MaxValue || disk == ushort.MaxValue;
        for (int at = 0; at < bytes.Length;)
        {
            Require(bytes.Length - at >= 4); int id = U16(bytes, at), length = U16(bytes, at + 2); at += 4;
            Require(length <= bytes.Length - at);
            // v1 producer needs only ZIP64. Reject alternative path/link/security metadata rather than interpreting it.
            Require(id == 1 && !found); found = true;
            int stop = at + length;
            if (size == uint.MaxValue) size = ExtraLong(bytes, ref at, stop);
            if (compressed == uint.MaxValue) compressed = ExtraLong(bytes, ref at, stop);
            if (offset == uint.MaxValue) offset = ExtraLong(bytes, ref at, stop);
            if (disk == ushort.MaxValue) { Require(stop - at >= 4); disk = U32(bytes, at); at += 4; }
            Require(at == stop);
        }
        Require(!requiresZip64 || found);
    }
    private static long ExtraLong(byte[] bytes, ref int position, int end)
    { Require(end - position >= 8); long value = Long(U64(bytes, position)); position += 8; return value; }
    internal static long Maximum(string name, PreflightLimits limits) => name switch
    {
        BackupArchiveContract.ManifestPath => limits.ManifestBytes,
        BackupArchiveContract.DatabasePath => limits.DatabaseBytes,
        BackupArchiveContract.PreferencesPath => BackupArchiveContract.MaximumPreferencesBytes,
        _ when name.StartsWith(BackupArchiveContract.ScreenshotsPrefix, StringComparison.Ordinal) &&
            BackupManifestValidator.IsPortableScreenshotKey(name[BackupArchiveContract.ScreenshotsPrefix.Length..]) => limits.ScreenshotBytes,
        _ => throw new PreflightFailure(BackupValidationCode.UnexpectedEntry)
    };
    private static byte[] ReadAt(FileStream stream, long offset, int count)
    { Require(offset >= 0 && offset <= stream.Length - count); stream.Position = offset; return ReadWithin(stream, count, stream.Length); }
    private static byte[] ReadWithin(FileStream stream, int count, long end)
    { Require(count >= 0 && count <= end - stream.Position); var bytes = new byte[count]; stream.ReadExactly(bytes); return bytes; }
    private static ushort U16(ReadOnlySpan<byte> bytes, int at) => BinaryPrimitives.ReadUInt16LittleEndian(bytes[at..]);
    private static uint U32(ReadOnlySpan<byte> bytes, int at) => BinaryPrimitives.ReadUInt32LittleEndian(bytes[at..]);
    private static ulong U64(ReadOnlySpan<byte> bytes, int at) => BinaryPrimitives.ReadUInt64LittleEndian(bytes[at..]);
    private static long Long(ulong value) { Require(value <= long.MaxValue); return (long)value; }
    private static void Require(bool valid) { if (!valid) throw new PreflightFailure(BackupValidationCode.IncompleteArchive); }
    private static void Limit(bool valid) { if (!valid) throw new PreflightFailure(BackupValidationCode.LimitExceeded); }
}

internal sealed class PreflightFailure(BackupValidationCode code) : Exception { internal BackupValidationCode Code { get; } = code; }
internal sealed record PreflightLimits(int Files = BackupArchiveContract.MaximumFiles, int ManifestBytes = BackupArchiveContract.MaximumManifestBytes,
    long ArchiveBytes = BackupArchiveContract.MaximumArchiveBytes, long DatabaseBytes = BackupArchiveContract.MaximumDatabaseBytes,
    long ScreenshotBytes = BackupArchiveContract.MaximumScreenshotBytes, long PayloadBytes = BackupArchiveContract.MaximumPayloadBytes,
    int DatabaseProgressCallbacks = 100000, int DatabaseSeconds = 60, int DatabaseProgressInterval = 1000);
