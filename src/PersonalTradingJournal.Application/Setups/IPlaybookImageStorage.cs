using PersonalTradingJournal.Domain.Setups;

namespace PersonalTradingJournal.Application.Setups;

public interface IPlaybookImageStorage
{
    Task<PlaybookExampleImage> StoreAsync(string fileName, ReadOnlyMemory<byte> bytes, CancellationToken token = default);
    /// <summary>Returns bounded original bytes only if their frozen size/hash still match.</summary>
    Task<byte[]> ReadAsync(PlaybookExampleImage image, CancellationToken token = default);
    /// <summary>Compensation only. Retain any file referenced by a Trade or any Playbook revision.</summary>
    Task DeleteUnreferencedAsync(string storageKey, CancellationToken token = default);
}

/// <summary>The host retains the selected bytes until the complete revision commits or editing is discarded.</summary>
public sealed record NewPlaybookImage(Guid ExampleId, string FileName, ReadOnlyMemory<byte> Bytes);

public sealed class PlaybookImageCleanupException : IOException
{
    public PlaybookImageCleanupException() : base("The revision was not saved and temporary image cleanup could not be completed. Your edits are retained; unused image files may remain.") { }
}
