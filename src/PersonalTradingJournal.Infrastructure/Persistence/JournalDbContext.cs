using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Infrastructure.Persistence.Configurations;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Persistence;

public sealed class JournalDbContext : DbContext
{
    public JournalDbContext(DbContextOptions<JournalDbContext> options)
        : base(options)
    {
    }

    public DbSet<DailyTradingPrincipleRecord> DailyTradingPrinciples => Set<DailyTradingPrincipleRecord>();
    public DbSet<NotebookNoteRecord> NotebookNotes => Set<NotebookNoteRecord>();
    public DbSet<NotebookRecoveryRecord> NotebookRecoveryDrafts => Set<NotebookRecoveryRecord>();
    public DbSet<NotebookFolderRecord> NotebookFolders => Set<NotebookFolderRecord>();
    public DbSet<NotebookTagRecord> NotebookTags => Set<NotebookTagRecord>();
    public DbSet<NotebookTagAssignmentRecord> NotebookTagAssignments => Set<NotebookTagAssignmentRecord>();
    public DbSet<NotebookLinkRecord> NotebookLinks => Set<NotebookLinkRecord>();
    public DbSet<NotebookImageRecord> NotebookImages => Set<NotebookImageRecord>();
    public DbSet<DailyTradingPrincipleStateRecord> DailyTradingPrincipleState => Set<DailyTradingPrincipleStateRecord>();

    public DbSet<InstrumentRecord> Instruments => Set<InstrumentRecord>();
    public DbSet<TradingPlanRecord> TradingPlans => Set<TradingPlanRecord>();
    public DbSet<TradingPlanReadyRecord> TradingPlanReadyRevisions => Set<TradingPlanReadyRecord>();
    public DbSet<TradingPlanAssetRecord> TradingPlanAssets => Set<TradingPlanAssetRecord>();

    public DbSet<TradingAccountRecord> TradingAccounts => Set<TradingAccountRecord>();

    public DbSet<TradingRuleRevisionRecord> TradingRuleRevisions => Set<TradingRuleRevisionRecord>();
    public DbSet<TradingRulePositionRecord> TradingRulePositions => Set<TradingRulePositionRecord>();
    public DbSet<TradingRuleCatalogStateRecord> TradingRuleCatalogState => Set<TradingRuleCatalogStateRecord>();
    public DbSet<TradingRuleAssessmentRecord> TradingRuleAssessments => Set<TradingRuleAssessmentRecord>();

    public DbSet<DailyJournalRecord> DailyJournals => Set<DailyJournalRecord>();

    public DbSet<CoachingAnalysisRecord> CoachingAnalyses => Set<CoachingAnalysisRecord>();
    public DbSet<PeriodReviewRecord> PeriodReviews => Set<PeriodReviewRecord>();

    public DbSet<DailyJournalRevisionRecord> DailyJournalRevisions => Set<DailyJournalRevisionRecord>();

    public DbSet<TradingSetupRecord> TradingSetups => Set<TradingSetupRecord>();

    public DbSet<PlaybookRevisionRecord> PlaybookRevisions => Set<PlaybookRevisionRecord>();
    public DbSet<CurrentPlaybookRecord> CurrentPlaybooks => Set<CurrentPlaybookRecord>();
    public DbSet<CurrentPlaybookExampleRecord> CurrentPlaybookExamples => Set<CurrentPlaybookExampleRecord>();
    public DbSet<PlaybookExampleRecord> PlaybookExamples => Set<PlaybookExampleRecord>();
    public DbSet<TradePlaybookContextRecord> TradePlaybookContexts => Set<TradePlaybookContextRecord>();
    public DbSet<TradePlaybookAssociationRecord> TradePlaybookAssociations => Set<TradePlaybookAssociationRecord>();

    public DbSet<TradingMistakeRecord> TradingMistakes => Set<TradingMistakeRecord>();

    public DbSet<TradeRecord> Trades => Set<TradeRecord>();

    public DbSet<TradeBrowseRecord> TradeBrowse => Set<TradeBrowseRecord>();

    public DbSet<TradeExecutionRecord> TradeExecutions => Set<TradeExecutionRecord>();

    public DbSet<TradeScreenshotRecord> TradeScreenshots => Set<TradeScreenshotRecord>();

    public DbSet<TradeMistakeRecord> TradeMistakes => Set<TradeMistakeRecord>();

    public DbSet<TopstepImportedRowRecord> TopstepImportedRows => Set<TopstepImportedRowRecord>();

    public DbSet<TradovateImportedExecutionRecord> TradovateImportedExecutions =>
        Set<TradovateImportedExecutionRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        NotebookConfiguration.Configure(modelBuilder);
        modelBuilder.Entity<DailyTradingPrincipleRecord>(b =>
        {
            b.ToTable("DailyTradingPrinciples", t => t.HasCheckConstraint("CK_DailyTradingPrinciples_Content", "length(trim(Text)) > 0 AND length(Text) <= 500 AND Position > 0"));
            b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Text).IsRequired().HasMaxLength(500);
            b.HasIndex(x => x.Position).IsUnique();
            b.Property(x => x.CreatedAtUtc).HasConversion<PersonalTradingJournal.Infrastructure.Persistence.Converters.SqliteUtcDateTimeOffsetConverter>();
        });
        modelBuilder.Entity<DailyTradingPrincipleStateRecord>(b =>
        {
            b.ToTable("DailyTradingPrincipleState", t => t.HasCheckConstraint("CK_DailyTradingPrincipleState_Singleton", "Id = 1 AND Version >= 0"));
            b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Version).IsConcurrencyToken();
            b.HasData(new DailyTradingPrincipleStateRecord { Id = 1, Version = 0 });
        });
        modelBuilder.Entity<CurrentPlaybookRecord>(b =>
        {
            b.ToTable("CurrentPlaybooks", t => t.HasCheckConstraint("CK_CurrentPlaybooks_Content",
                "Version > 0 AND ((LegacyRevision IS NOT NULL AND LegacyRevision = Version AND DefinitionJson IS NULL) OR (LegacyRevision IS NULL AND DefinitionJson IS NOT NULL))"));
            b.HasKey(x => x.TradingSetupId); b.Property(x => x.TradingSetupId).ValueGeneratedNever();
            b.Property(x => x.Version).IsConcurrencyToken();
            b.HasOne<TradingSetupRecord>().WithMany().HasForeignKey(x => x.TradingSetupId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<PlaybookRevisionRecord>().WithMany().HasForeignKey(x => new { x.TradingSetupId, Revision = x.LegacyRevision }).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<CurrentPlaybookExampleRecord>(b =>
        {
            b.ToTable("CurrentPlaybookExamples"); b.HasKey(x => new { x.TradingSetupId, x.ExampleId });
            b.Property(x => x.ExampleId).ValueGeneratedNever();
            b.HasOne<CurrentPlaybookRecord>().WithMany().HasForeignKey(x => x.TradingSetupId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<TradeScreenshotRecord>().WithMany().HasForeignKey(x => x.SourceScreenshotId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.ApplyConfiguration<TradingPlanRecord>(new TradingPlanConfiguration());
        modelBuilder.ApplyConfiguration<TradingPlanReadyRecord>(new TradingPlanConfiguration());
        modelBuilder.Entity<TradingPlanAssetRecord>(b =>
        {
            b.ToTable("TradingPlanAssets"); b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
            b.HasIndex(x => x.StorageKey).IsUnique();
            b.HasOne<TradingPlanRecord>().WithMany().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.ApplyConfiguration(new InstrumentRecordConfiguration());
        modelBuilder.ApplyConfiguration(new TradingAccountRecordConfiguration());
        modelBuilder.ApplyConfiguration(new TradingRuleRevisionRecordConfiguration());
        modelBuilder.ApplyConfiguration(new TradingRulePositionConfiguration());
        modelBuilder.ApplyConfiguration(new TradingRuleCatalogStateConfiguration());
        modelBuilder.ApplyConfiguration(new TradingRuleAssessmentRecordConfiguration());
        modelBuilder.ApplyConfiguration(new DailyJournalRecordConfiguration());
        modelBuilder.ApplyConfiguration(new CoachingAnalysisRecordConfiguration());
        modelBuilder.ApplyConfiguration(new PeriodReviewRecordConfiguration());
        modelBuilder.ApplyConfiguration(new DailyJournalRevisionRecordConfiguration());
        modelBuilder.ApplyConfiguration(new TradingSetupRecordConfiguration());
        modelBuilder.ApplyConfiguration(new PlaybookRevisionRecordConfiguration());
        modelBuilder.ApplyConfiguration(new PlaybookExampleRecordConfiguration());
        modelBuilder.ApplyConfiguration(new TradePlaybookContextRecordConfiguration());
        modelBuilder.ApplyConfiguration(new TradePlaybookAssociationRecordConfiguration());
        modelBuilder.ApplyConfiguration(new TradingMistakeRecordConfiguration());
        modelBuilder.ApplyConfiguration(new TradeRecordConfiguration());
        modelBuilder.ApplyConfiguration(new TradeBrowseRecordConfiguration());
        modelBuilder.ApplyConfiguration(new TradeExecutionRecordConfiguration());
        modelBuilder.ApplyConfiguration(new TradeScreenshotRecordConfiguration());
        modelBuilder.ApplyConfiguration(new TradeMistakeRecordConfiguration());
        modelBuilder.ApplyConfiguration(new TopstepImportedRowRecordConfiguration());
        modelBuilder.ApplyConfiguration(
            new TradovateImportedExecutionRecordConfiguration());
    }
}
