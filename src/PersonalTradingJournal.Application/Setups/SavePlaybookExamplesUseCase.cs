using PersonalTradingJournal.Domain.Setups;

namespace PersonalTradingJournal.Application.Setups;

/// <summary>File publication precedes the existing atomic metadata/revision append. Never deletes a historical reference.</summary>
public sealed class SavePlaybookExamplesUseCase(SaveTradingSetupPlaybookUseCase save, IPlaybookImageStorage images)
{
    public async Task<TradingSetupPlaybookRevision> ExecuteAsync(SaveTradingSetupPlaybookCommand command,
        IReadOnlyList<NewPlaybookImage> pending, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(command); ArgumentNullException.ThrowIfNull(pending);
        if (pending.Count > PlaybookGuidance.MaximumExamples || pending.Select(p => p.ExampleId).Distinct().Count() != pending.Count)
            throw new ArgumentException("Example image identities must be unique and bounded.");
        foreach (var image in pending)
        {
            PlaybookExampleImage.ValidateFileName(image.FileName);
            var example = command.Guidance.Examples.SingleOrDefault(e => e.Id == image.ExampleId);
            if (image.Bytes.Length is <= 0 or > PlaybookExampleImage.MaximumBytes || example is null ||
                example.Image is not null || example.SourceScreenshotId.HasValue || example.Kind == PlaybookExampleKind.Unspecified)
                throw new ArgumentException("Each new image needs a matching valid or invalid annotated example.");
        }
        var created = new List<string>();
        try
        {
            var examples = command.Guidance.Examples.ToArray();
            foreach (var image in pending)
            {
                token.ThrowIfCancellationRequested();
                var stored = await images.StoreAsync(image.FileName, image.Bytes, token);
                created.Add(stored.StorageKey);
                int index = Array.FindIndex(examples, e => e.Id == image.ExampleId);
                var original = examples[index];
                examples[index] = new(original.Id, original.Annotation, original.SourceTradeId, original.SourceScreenshotId, original.Kind, stored);
            }
            var g = command.Guidance;
            var complete = new PlaybookGuidance(g.MarketContext, g.EntryConditions, g.Confirmations, g.Invalidation, g.ExecutionGuidance, examples);
            return await save.ExecuteAsync(command with { Guidance = complete }, token);
        }
        catch
        {
            bool cleanupFailed = false;
            foreach (string key in created)
                try { await images.DeleteUnreferencedAsync(key, CancellationToken.None); }
                catch (Exception) { cleanupFailed = true; }
            if (cleanupFailed) throw new PlaybookImageCleanupException();
            throw;
        }
    }
}
