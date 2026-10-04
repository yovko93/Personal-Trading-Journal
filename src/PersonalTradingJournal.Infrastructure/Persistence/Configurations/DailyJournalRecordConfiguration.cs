using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalTradingJournal.Domain.Journals;
using PersonalTradingJournal.Infrastructure.Persistence.Converters;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Persistence.Configurations;

public sealed class DailyJournalRecordConfiguration : IEntityTypeConfiguration<DailyJournalRecord>
{
    public void Configure(EntityTypeBuilder<DailyJournalRecord> builder)
    {
        builder.ToTable("DailyJournals", table =>
            table.HasCheckConstraint("CK_DailyJournals_Revision", "\"Revision\" >= 1"));
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Text).IsRequired().HasMaxLength(DailyJournalEntry.MaximumTextLength);
        builder.Property(r => r.Revision).IsConcurrencyToken();
        builder.Property(r => r.CreatedAtUtc).HasConversion<SqliteUtcDateTimeOffsetConverter>();
        builder.Property(r => r.UpdatedAtUtc).HasConversion<SqliteUtcDateTimeOffsetConverter>();
        builder.HasOne<TradingAccountRecord>().WithMany().HasForeignKey(r => r.TradingAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        // SQLite treats NULLs as distinct in a composite unique index. The second
        // index is essential: All accounts is one explicit scope, not a wildcard.
        builder.HasIndex(r => new { r.TradingDate, r.TradingAccountId }).IsUnique()
            .HasFilter("\"TradingAccountId\" IS NOT NULL");
        builder.HasIndex(r => r.TradingDate).IsUnique()
            .HasFilter("\"TradingAccountId\" IS NULL");
    }
}
