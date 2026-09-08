using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace PersonalTradingJournal.Infrastructure.Persistence.Converters;

public sealed class SqliteUtcDateTimeOffsetConverter :
    ValueConverter<DateTimeOffset, DateTime>
{
    public SqliteUtcDateTimeOffsetConverter()
        : base(
            value => ConvertUtcToProvider(value),
            value => ConvertProviderToUtc(value))
    {
    }

    private static DateTime ConvertUtcToProvider(DateTimeOffset value)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "SQLite persistence requires a UTC offset of zero.",
                nameof(value));
        }

        return value.UtcDateTime;
    }

    private static DateTimeOffset ConvertProviderToUtc(DateTime value)
    {
        return new DateTimeOffset(value.Ticks, TimeSpan.Zero);
    }
}
