using PersonalTradingJournal.Domain.Setups;

namespace PersonalTradingJournal.Application.Setups;

public sealed record TradingSetupDetails(
    Guid Id,
    string Name,
    string? Description,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc)
{
    internal static TradingSetupDetails FromDomain(TradingSetup setup)
    {
        ArgumentNullException.ThrowIfNull(setup);
        return new TradingSetupDetails(
            setup.Id,
            setup.Name,
            setup.Description,
            setup.IsActive,
            setup.CreatedAtUtc,
            setup.UpdatedAtUtc);
    }
}
