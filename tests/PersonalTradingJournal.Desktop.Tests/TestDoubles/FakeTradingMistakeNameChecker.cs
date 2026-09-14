using PersonalTradingJournal.Application.Mistakes;

namespace PersonalTradingJournal.Desktop.Tests.TestDoubles;

internal sealed class FakeTradingMistakeNameChecker : ITradingMistakeNameChecker
{
    public bool Exists { get; set; }
    public Task<bool> ExistsAsync(string name, Guid? excludingMistakeId = null, CancellationToken token = default) => Task.FromResult(Exists);
}
