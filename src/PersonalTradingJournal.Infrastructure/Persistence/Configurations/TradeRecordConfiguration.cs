using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Persistence.Configurations;

public sealed class TradeRecordConfiguration : IEntityTypeConfiguration<TradeRecord>
{
    public void Configure(EntityTypeBuilder<TradeRecord> builder)
    {
        builder.ToTable("Trades");

        builder.HasKey(record => record.Id);

        builder.Property(record => record.Id)
            .ValueGeneratedNever();

        builder.Property(record => record.TradingAccountId)
            .IsRequired();

        builder.Property(record => record.InstrumentId)
            .IsRequired();

        builder.Property(record => record.PricingPointValue)
            .IsRequired();

        builder.Property(record => record.PricingCurrency)
            .IsRequired()
            .HasMaxLength(8);

        builder.Property(record => record.StrategyId)
            .IsRequired(false);

        builder.Property(record => record.TradingSetupId)
            .IsRequired(false);

        builder.Property(record => record.CreatedAtUtc)
            .IsRequired();

        builder.Property(record => record.UpdatedAtUtc)
            .IsRequired();

        builder.HasOne<TradingAccountRecord>()
            .WithMany()
            .HasForeignKey(record => record.TradingAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<InstrumentRecord>()
            .WithMany()
            .HasForeignKey(record => record.InstrumentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<StrategyRecord>()
            .WithMany()
            .HasForeignKey(record => record.StrategyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<TradingSetupRecord>()
            .WithMany()
            .HasForeignKey(record => record.TradingSetupId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
