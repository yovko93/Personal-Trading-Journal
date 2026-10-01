using PersonalTradingJournal.Application.Analytics;

namespace PersonalTradingJournal.Desktop.Tests.TestDoubles;

internal sealed class FakeDashboardAnalyticsReader : IDashboardAnalyticsReader
{
    public List<DashboardAnalyticsQuery> Queries { get; } = [];
    public Func<DashboardAnalyticsQuery, CancellationToken, Task<DashboardAnalyticsSnapshot>> Read { get; set; } =
        (_, _) => Task.FromResult(DashboardMetricCalculator.Calculate([]));
    public Task<DashboardAnalyticsSnapshot> GetAsync(DashboardAnalyticsQuery query, CancellationToken cancellationToken = default)
    {
        Queries.Add(query);
        return Read(query, cancellationToken);
    }
}
