using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalTradingJournal.Infrastructure.Persistence.Converters;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Persistence.Configurations;

public sealed class TradovateImportedExecutionRecordConfiguration :
    IEntityTypeConfiguration<TradovateImportedExecutionRecord>
{
    public void Configure(EntityTypeBuilder<TradovateImportedExecutionRecord> builder)
    {
        builder.ToTable("TradovateImportedExecutions");

        builder.HasKey(record => record.TradeExecutionId);

        builder.Property(record => record.TradeExecutionId)
            .ValueGeneratedNever();
        builder.Property(record => record.TradeId).IsRequired();
        builder.Property(record => record.TradingAccountIdAtImport).IsRequired();
        builder.Property(record => record.BrokerSymbol)
            .IsRequired()
            .HasMaxLength(64);
        builder.Property(record => record.Side)
            .IsRequired()
            .HasConversion<int>();
        builder.Property(record => record.ExternalExecutionId)
            .IsRequired()
            .HasMaxLength(128);
        builder.Property(record => record.ImportedAtUtc)
            .IsRequired()
            .HasConversion<SqliteUtcDateTimeOffsetConverter>();

        builder.HasOne<TradeRecord>()
            .WithMany()
            .HasForeignKey(record => record.TradeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(record => new
            {
                record.TradingAccountIdAtImport,
                record.BrokerSymbol,
                record.Side,
                record.ExternalExecutionId,
            })
            .IsUnique();
    }
}
