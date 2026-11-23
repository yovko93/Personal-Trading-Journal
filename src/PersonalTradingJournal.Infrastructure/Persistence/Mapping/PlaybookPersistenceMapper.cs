using System.Text.Json;
using System.Text.Json.Serialization;
using PersonalTradingJournal.Domain.Setups;

namespace PersonalTradingJournal.Infrastructure.Persistence.Mapping;

internal static class PlaybookPersistenceMapper
{
    // Optional version-1 extension; omitted defaults preserve canonical legacy snapshot JSON.
    private sealed record Example(Guid Id, string Annotation, Guid? SourceTradeId, Guid? SourceScreenshotId,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] PlaybookExampleKind Kind = PlaybookExampleKind.Unspecified,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] PlaybookExampleImage? Image = null);
    private sealed record Definition(int Version, Guid SetupId, int Revision, string Name, string? Description,
        DateTimeOffset RecordedAtUtc, string MarketContext, string EntryConditions, string Confirmations,
        string Invalidation, string ExecutionGuidance, Example[] Examples);

    internal static string Serialize(TradingSetupPlaybookRevision value) => JsonSerializer.Serialize(new Definition(
        TradingSetupPlaybookRevision.ContractVersion, value.TradingSetupId, value.Revision, value.SetupName,
        value.SetupDescription, value.RecordedAtUtc, value.Guidance.MarketContext, value.Guidance.EntryConditions,
        value.Guidance.Confirmations, value.Guidance.Invalidation, value.Guidance.ExecutionGuidance,
        value.Guidance.Examples.Select(e => new Example(e.Id, e.Annotation, e.SourceTradeId, e.SourceScreenshotId, e.Kind, e.Image)).ToArray()));

    internal static TradingSetupPlaybookRevision Deserialize(string json)
    {
        if (json.Length > 1_000_000) throw new InvalidDataException("Playbook snapshot exceeds its contract limit.");
        Definition value = JsonSerializer.Deserialize<Definition>(json, new JsonSerializerOptions { MaxDepth = 16 })
            ?? throw new InvalidDataException("Missing Playbook snapshot.");
        if (value.Version != TradingSetupPlaybookRevision.ContractVersion || value.Examples is null)
            throw new InvalidDataException("Unsupported Playbook snapshot.");
        return TradingSetupPlaybookRevision.Rehydrate(value.SetupId, value.Revision, value.Name, value.Description,
            new(value.MarketContext, value.EntryConditions, value.Confirmations, value.Invalidation,
                value.ExecutionGuidance, value.Examples.Select(e => new PlaybookExample(e.Id, e.Annotation, e.SourceTradeId, e.SourceScreenshotId, e.Kind, e.Image))),
            value.RecordedAtUtc);
    }
}
