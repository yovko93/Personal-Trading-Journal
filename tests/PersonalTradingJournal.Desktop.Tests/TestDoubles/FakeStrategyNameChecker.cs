using PersonalTradingJournal.Application.Strategies;

namespace PersonalTradingJournal.Desktop.Tests.TestDoubles;

internal sealed class FakeStrategyNameChecker : IStrategyNameChecker
{
    public bool Exists { get; set; }

    public string? NormalizedName { get; private set; }

    public Task<bool> ExistsAsync(
        string normalizedName,
        Guid? excludingStrategyId = null,
        CancellationToken cancellationToken = default)
    {
        NormalizedName = normalizedName;
        return Task.FromResult(Exists);
    }
}
