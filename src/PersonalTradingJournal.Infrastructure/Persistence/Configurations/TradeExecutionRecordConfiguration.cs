using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Persistence.Configurations;

public sealed class TradeExecutionRecordConfiguration :
    IEntityTypeConfiguration<TradeExecutionRecord>
{
    public void Configure(EntityTypeBuilder<TradeExecutionRecord> builder)
    {
        builder.ToTable("TradeExecutions");

        builder.HasKey(record => record.Id);

        builder.Property(record => record.Id)
            .ValueGeneratedNever();

        builder.Property(record => record.TradeId)
            .IsRequired();

        builder.Property(record => record.Sequence)
            .IsRequired();

        builder.Property(record => record.ExecutedAtUtc)
            .IsRequired();

        builder.Property(record => record.Side)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(record => record.Quantity)
            .IsRequired();

        builder.Property(record => record.Price)
            .IsRequired();

        builder.Property(record => record.Commission)
            .IsRequired();

        builder.Property(record => record.Fees)
            .IsRequired();

        builder.Property(record => record.ExternalExecutionId)
            .IsRequired(false)
            .HasMaxLength(128);

        builder.Property(record => record.ExternalOrderId)
            .IsRequired(false)
            .HasMaxLength(128);

        builder.Property(record => record.BrokerSymbol)
            .IsRequired(false)
            .HasMaxLength(64);

        builder.HasOne<TradeRecord>()
            .WithMany()
            .HasForeignKey(record => record.TradeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(record => new
            {
                record.TradeId,
                record.Sequence,
            })
            .IsUnique();
    }
}
