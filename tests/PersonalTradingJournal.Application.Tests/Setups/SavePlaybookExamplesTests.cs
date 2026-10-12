using System.Security.Cryptography;
using PersonalTradingJournal.Application.Setups;
using PersonalTradingJournal.Domain.Setups;

namespace PersonalTradingJournal.Application.Tests.Setups;

public sealed partial class SaveTradingSetupPlaybookUseCaseTests
{
    [Theory]
    [InlineData("success")] [InlineData("stale")] [InlineData("failure")] [InlineData("cancel-after-file")]
    [InlineData("second-file-failure")] [InlineData("cleanup-failure")]
    public async Task ImageWorkflowCommitsOrCompensatesWithoutChangingPendingBytes(string outcome)
    {
        using var cancel = new CancellationTokenSource();
        var store = new Store { RejectAppend = outcome is "stale" or "cleanup-failure", FailWrite = outcome == "failure" };
        var images = new FakeImages { Cancel = outcome == "cancel-after-file" ? cancel : null,
            FailSecond = outcome == "second-file-failure", FailCleanup = outcome == "cleanup-failure" };
        var workflow = new SavePlaybookExamplesUseCase(new(store, store, new Clock()), images);
        Guid a = Guid.NewGuid(), b = Guid.NewGuid(); byte[] bytes = [1, 2, 3];
        var guidance = new PlaybookGuidance("c", "e", "c", "i", "x", [new(a, "valid note", kind: PlaybookExampleKind.Valid), new(b, "invalid note", kind: PlaybookExampleKind.Invalid)]);
        Task<TradingSetupPlaybookRevision> Save() => workflow.ExecuteAsync(new(store.Setup!.Id, 0, guidance), [new(a, "one.png", bytes), new(b, "two.png", bytes)], cancel.Token);
        if (outcome == "success")
        {
            var saved = await Save(); Assert.Single(store.History); Assert.Equal(2, images.Files.Count);
            Assert.All(saved.Guidance.Examples, e => Assert.NotNull(e.Image)); Assert.Empty(images.Deleted);
        }
        else
        {
            var exception = await Assert.ThrowsAnyAsync<Exception>(Save); Assert.Empty(store.History);
            if (outcome == "cleanup-failure") { Assert.IsType<PlaybookImageCleanupException>(exception); Assert.Equal(2, images.Files.Count); }
            else Assert.Empty(images.Files);
            Assert.NotEmpty(images.Deleted); Assert.All(images.CleanupTokens, t => Assert.False(t.CanBeCanceled));
        }
        Assert.Equal(new byte[] { 1, 2, 3 }, bytes); Assert.All(guidance.Examples, e => Assert.Null(e.Image));
    }

    [Fact]
    public async Task InvalidPendingImagesAndEarlyCancellationDoNotPublishFiles()
    {
        var store = new Store(); var images = new FakeImages(); var workflow = new SavePlaybookExamplesUseCase(new(store, store, new Clock()), images);
        Guid id = Guid.NewGuid(); var g = new PlaybookGuidance("c", "e", "c", "i", "x", [new(id, "note", kind: PlaybookExampleKind.Valid)]);
        foreach (var pending in new[] { new NewPlaybookImage(id, "../bad.png", new byte[] { 1 }), new(id, "empty.png", Array.Empty<byte>()), new(Guid.NewGuid(), "wrong.png", new byte[] { 1 }) })
            await Assert.ThrowsAnyAsync<ArgumentException>(() => workflow.ExecuteAsync(new(store.Setup!.Id, 0, g), [pending]));
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => workflow.ExecuteAsync(new(store.Setup!.Id, 0, g), [new(id, "good.png", new byte[] { 1 })], cancel.Token));
        Assert.Empty(images.Files); Assert.Empty(store.History);
    }

    private sealed class FakeImages : IPlaybookImageStorage
    {
        internal readonly Dictionary<string, byte[]> Files = [];
        internal readonly List<string> Deleted = [];
        internal readonly List<CancellationToken> CleanupTokens = [];
        internal CancellationTokenSource? Cancel;
        internal bool FailSecond, FailCleanup;
        public Task<PlaybookExampleImage> StoreAsync(string name, ReadOnlyMemory<byte> bytes, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested(); if (FailSecond && Files.Count == 1) throw new IOException("synthetic");
            string key = Guid.NewGuid().ToString("N") + ".png"; Files[key] = bytes.ToArray(); Cancel?.Cancel();
            return Task.FromResult(new PlaybookExampleImage(key, name, bytes.Length, Convert.ToHexString(SHA256.HashData(bytes.Span))));
        }
        public Task<byte[]> ReadAsync(PlaybookExampleImage image, CancellationToken token = default) => Task.FromResult(Files[image.StorageKey]);
        public Task DeleteUnreferencedAsync(string key, CancellationToken token = default)
        { Deleted.Add(key); CleanupTokens.Add(token); if (FailCleanup) throw new IOException("synthetic"); Files.Remove(key); return Task.CompletedTask; }
    }
}
