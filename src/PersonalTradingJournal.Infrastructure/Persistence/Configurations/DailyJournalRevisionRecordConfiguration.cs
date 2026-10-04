using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalTradingJournal.Domain.Journals;
using PersonalTradingJournal.Infrastructure.Persistence.Converters;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Persistence.Configurations;

public sealed class DailyJournalRevisionRecordConfiguration : IEntityTypeConfiguration<DailyJournalRevisionRecord>
{
    public void Configure(EntityTypeBuilder<DailyJournalRevisionRecord> builder)
    {
        builder.ToTable("DailyJournalRevisions", table =>
            table.HasCheckConstraint("CK_DailyJournalRevisions_Revision", "\"Revision\" >= 1"));
        builder.HasKey(r => new { r.JournalId, r.Revision });
        builder.Property(r => r.JournalId).ValueGeneratedNever();
        builder.Property(r => r.Revision).ValueGeneratedNever();
        builder.Property(r => r.Text).IsRequired().HasMaxLength(DailyJournalEntry.MaximumTextLength);
        builder.Property(r => r.SavedAtUtc).HasConversion<SqliteUtcDateTimeOffsetConverter>();
        builder.HasOne<DailyJournalRecord>().WithMany().HasForeignKey(r => r.JournalId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
