using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalTradingJournal.Infrastructure.Persistence.Converters;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Persistence.Configurations;

public sealed class TradingMistakeRecordConfiguration :
    IEntityTypeConfiguration<TradingMistakeRecord>
{
    public void Configure(EntityTypeBuilder<TradingMistakeRecord> builder)
    {
        builder.ToTable("TradingMistakes");

        builder.HasKey(record => record.Id);

        builder.Property(record => record.Id)
            .ValueGeneratedNever();

        builder.Property(record => record.Name)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(record => record.Description)
            .IsRequired(false)
            .HasMaxLength(2000);

        builder.Property(record => record.IsActive)
            .IsRequired();

        builder.Property(record => record.CreatedAtUtc)
            .IsRequired()
            .HasConversion<SqliteUtcDateTimeOffsetConverter>();

        builder.Property(record => record.UpdatedAtUtc)
            .IsRequired()
            .HasConversion<SqliteUtcDateTimeOffsetConverter>();
    }
}
