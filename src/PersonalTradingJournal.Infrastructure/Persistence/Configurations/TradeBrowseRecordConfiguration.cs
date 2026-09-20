using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalTradingJournal.Infrastructure.Persistence.Converters;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Persistence.Configurations;

public sealed class TradeBrowseRecordConfiguration :
    IEntityTypeConfiguration<TradeBrowseRecord>
{
    private const int DecimalSortKeyLength = 58;

    public void Configure(EntityTypeBuilder<TradeBrowseRecord> builder)
    {
        builder.ToTable("TradeBrowse");

        builder.HasKey(record => record.TradeId);

        builder.Property(record => record.TradeId)
            .ValueGeneratedNever();

        builder.Property(record => record.ProjectionVersion)
            .IsRequired();

        builder.Property(record => record.OpenedAtUtc)
            .IsRequired()
            .HasConversion<SqliteUtcDateTimeOffsetConverter>();

        builder.Property(record => record.ClosedAtUtc)
            .IsRequired(false)
            .HasConversion<SqliteUtcDateTimeOffsetConverter>();

        builder.Property(record => record.Direction)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(record => record.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(record => record.OpenQuantity)
            .IsRequired();

        ConfigureSortKey(
            builder.Property(record => record.OpenQuantitySortKey));

        builder.Property(record => record.AverageEntryPrice)
            .IsRequired();

        ConfigureSortKey(
            builder.Property(record => record.AverageEntryPriceSortKey));

        builder.Property(record => record.AverageExitPrice)
            .IsRequired(false);

        builder.Property(record => record.TotalCosts)
            .IsRequired();

        builder.Property(record => record.GrossPnL)
            .IsRequired(false);

        builder.Property(record => record.NetPnL)
            .IsRequired(false);

        ConfigureSortKey(
            builder.Property(record => record.NetPnLSortKey),
            required: false);

        builder.HasOne<TradeRecord>()
            .WithOne()
            .HasForeignKey<TradeBrowseRecord>(record => record.TradeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(record => record.OpenedAtUtc);
        builder.HasIndex(record => record.AverageEntryPriceSortKey);
        builder.HasIndex(record => record.OpenQuantitySortKey);
        builder.HasIndex(record => record.NetPnLSortKey);
    }

    private static void ConfigureSortKey(
        PropertyBuilder property,
        bool required = true)
    {
        property
            .IsRequired(required)
            .HasMaxLength(DecimalSortKeyLength)
            .UseCollation("BINARY");
    }
}
