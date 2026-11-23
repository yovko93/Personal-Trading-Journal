using PersonalTradingJournal.Domain.Setups;

namespace PersonalTradingJournal.Application.Setups;

public sealed record SetupExample(Guid Id, Guid SetupId, string? Timeframe, PlaybookExampleImage File);

public interface ISetupExampleStore
{
    Task<IReadOnlyList<SetupExample>> ListAsync(Guid setupId, CancellationToken token);
    Task<SetupExample> AddAsync(Guid setupId, string fileName, byte[] bytes, string? timeframe, CancellationToken token);
    Task RemoveAsync(Guid setupId, Guid imageId, CancellationToken token);
}
