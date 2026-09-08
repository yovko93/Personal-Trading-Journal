using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Persistence.Configurations;

public sealed class TradeScreenshotRecordConfiguration :
    IEntityTypeConfiguration<TradeScreenshotRecord>
{
    public void Configure(EntityTypeBuilder<TradeScreenshotRecord> builder)
    {
        builder.ToTable("TradeScreenshots");

        builder.HasKey(record => record.Id);

        builder.Property(record => record.Id)
            .ValueGeneratedNever();

        builder.Property(record => record.TradeId)
            .IsRequired();

        builder.Property(record => record.Type)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(record => record.StorageKey)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(record => record.FileName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(record => record.CapturedAtUtc)
            .IsRequired(false);

        builder.Property(record => record.Timeframe)
            .IsRequired(false)
            .HasMaxLength(32);

        builder.Property(record => record.Description)
            .IsRequired(false)
            .HasMaxLength(2000);

        builder.Property(record => record.CreatedAtUtc)
            .IsRequired();

        builder.Property(record => record.UpdatedAtUtc)
            .IsRequired();

        builder.HasOne<TradeRecord>()
            .WithMany()
            .HasForeignKey(record => record.TradeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
