using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalTradingJournal.Infrastructure.Persistence.Converters;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Persistence.Configurations;

public sealed class TopstepImportedRowRecordConfiguration : IEntityTypeConfiguration<TopstepImportedRowRecord>
{
    public void Configure(EntityTypeBuilder<TopstepImportedRowRecord> builder)
    {
        builder.ToTable("TopstepImportedRows");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.SourceId).IsRequired().UseCollation("BINARY");
        builder.Property(r => r.EconomicFingerprint).IsRequired().HasMaxLength(64);
        builder.Property(r => r.SourceRowJson).IsRequired();
        builder.Property(r => r.PreviewFingerprint).IsRequired().HasMaxLength(64);
        builder.Property(r => r.SourceContentSha256).IsRequired().HasMaxLength(64);
        builder.Property(r => r.Representation).IsRequired();
        builder.Property(r => r.ImportedAtUtc).HasConversion<SqliteUtcDateTimeOffsetConverter>();
        builder.HasIndex(r => new { r.TradingAccountIdAtImport, r.SourceId }).IsUnique();
        builder.HasIndex(r => r.TradeId).IsUnique();
        // Match the existing import lifecycle: hard-deleting a Trade removes its ledger identity.
        builder.HasOne<TradeRecord>().WithMany().HasForeignKey(r => r.TradeId).OnDelete(DeleteBehavior.Cascade);
    }
}
