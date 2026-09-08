using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Persistence.Configurations;

public sealed class TradeMistakeRecordConfiguration :
    IEntityTypeConfiguration<TradeMistakeRecord>
{
    public void Configure(EntityTypeBuilder<TradeMistakeRecord> builder)
    {
        builder.ToTable("TradeMistakes");

        builder.HasKey(record => record.Id);

        builder.Property(record => record.Id)
            .ValueGeneratedNever();

        builder.Property(record => record.TradeId)
            .IsRequired();

        builder.Property(record => record.TradingMistakeId)
            .IsRequired();

        builder.Property(record => record.Note)
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

        builder.HasOne<TradingMistakeRecord>()
            .WithMany()
            .HasForeignKey(record => record.TradingMistakeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(record => new
            {
                record.TradeId,
                record.TradingMistakeId,
            })
            .IsUnique();
    }
}
