namespace PersonalTradingJournal.Desktop.Tests.TestDoubles;

internal sealed class FixedTimeProvider : TimeProvider
{
    public static readonly DateTimeOffset FixedUtcNow =
        new(2026, 9, 9, 12, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => FixedUtcNow;
}
