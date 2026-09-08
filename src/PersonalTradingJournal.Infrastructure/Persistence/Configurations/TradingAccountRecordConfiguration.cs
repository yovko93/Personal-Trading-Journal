using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Persistence.Configurations;

public sealed class TradingAccountRecordConfiguration :
    IEntityTypeConfiguration<TradingAccountRecord>
{
    public void Configure(EntityTypeBuilder<TradingAccountRecord> builder)
    {
        builder.ToTable("TradingAccounts");

        builder.HasKey(record => record.Id);

        builder.Property(record => record.Id)
            .ValueGeneratedNever();

        builder.Property(record => record.Name)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(record => record.AccountType)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(record => record.ProviderName)
            .IsRequired(false)
            .HasMaxLength(128);

        builder.Property(record => record.ExternalAccountId)
            .IsRequired(false)
            .HasMaxLength(128);

        builder.Property(record => record.Currency)
            .IsRequired()
            .HasMaxLength(8);

        builder.Property(record => record.StartingBalance)
            .IsRequired(false);

        builder.Property(record => record.IsActive)
            .IsRequired();

        builder.Property(record => record.CreatedAtUtc)
            .IsRequired();

        builder.Property(record => record.UpdatedAtUtc)
            .IsRequired();
    }
}
