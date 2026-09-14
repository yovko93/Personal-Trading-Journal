using PersonalTradingJournal.Application.Setups;

namespace PersonalTradingJournal.Desktop.Tests.TestDoubles;

internal sealed class FakeTradingSetupNameChecker : ITradingSetupNameChecker
{
    public bool Exists { get; set; }
    public Task<bool> ExistsAsync(string normalizedName, Guid? excludingSetupId = null,
        CancellationToken cancellationToken = default) => Task.FromResult(Exists);
}
