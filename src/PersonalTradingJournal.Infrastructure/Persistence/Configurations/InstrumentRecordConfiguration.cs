using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Persistence.Configurations;

public sealed class InstrumentRecordConfiguration : IEntityTypeConfiguration<InstrumentRecord>
{
    public void Configure(EntityTypeBuilder<InstrumentRecord> builder)
    {
        builder.ToTable("Instruments");

        builder.HasKey(record => record.Id);

        builder.Property(record => record.Id)
            .ValueGeneratedNever();

        builder.Property(record => record.Symbol)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(record => record.DisplayName)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(record => record.AssetClass)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(record => record.Exchange)
            .IsRequired(false)
            .HasMaxLength(64);

        builder.Property(record => record.Currency)
            .IsRequired()
            .HasMaxLength(8);

        builder.Property(record => record.TickSize)
            .IsRequired();

        builder.Property(record => record.TickValue)
            .IsRequired();

        builder.Property(record => record.IsActive)
            .IsRequired();

        builder.Property(record => record.CreatedAtUtc)
            .IsRequired();

        builder.Property(record => record.UpdatedAtUtc)
            .IsRequired();
    }
}
