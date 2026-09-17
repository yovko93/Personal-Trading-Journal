using PersonalTradingJournal.Domain.Mistakes;

namespace PersonalTradingJournal.Application.Mistakes;

public sealed record TradingMistakeDetails(
    Guid Id,
    string Name,
    string? Description,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc)
{
    internal static TradingMistakeDetails FromDomain(TradingMistake mistake)
    {
        ArgumentNullException.ThrowIfNull(mistake);
        return new TradingMistakeDetails(
            mistake.Id,
            mistake.Name,
            mistake.Description,
            mistake.IsActive,
            mistake.CreatedAtUtc,
            mistake.UpdatedAtUtc);
    }
}
