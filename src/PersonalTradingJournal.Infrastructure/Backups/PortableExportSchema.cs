namespace PersonalTradingJournal.Infrastructure.Backups;

// Deliberate version-10 allowlist, not reflection over future DB columns. Any field/semantic change requires contract review.
internal enum ExportValueKind { Text, Id, Decimal, Integer, Boolean, Utc, NewYorkDate }
internal sealed record ExportColumn(string Column, ExportValueKind Kind, string? Alias = null)
{
    public string Name => Alias ?? char.ToLowerInvariant(Column[0]) + Column[1..];
}
internal sealed record ExportTable(string Name, string Source, string Order, bool Csv, ExportColumn[] Columns);
internal static class PortableExportSchema
{
    private static ExportColumn T(string n) => new(n, ExportValueKind.Text);
    private static ExportColumn I(string n) => new(n, ExportValueKind.Id);
    private static ExportColumn D(string n) => new(n, ExportValueKind.Decimal);
    private static ExportColumn N(string n) => new(n, ExportValueKind.Integer);
    private static ExportColumn B(string n) => new(n, ExportValueKind.Boolean);
    private static ExportColumn U(string n) => new(n, ExportValueKind.Utc);
    private static ExportColumn Y(string n, string alias) => new(n, ExportValueKind.NewYorkDate, alias);
    internal static readonly ExportTable[] Tables =
    [
        new("tradingPlans", "TradingPlans", "AccountId,NewYorkDate,Id", false,
            [I("Id"), I("AccountId"), Y("NewYorkDate", "newYorkDate"), N("Version"), N("LatestReadyRevision"),
             T("DraftJson"), T("ReferenceStateJson"), U("CreatedAtUtc"), U("UpdatedAtUtc")]),
        new("tradingPlanReadyRevisions", "TradingPlanReadyRevisions", "PlanId,Revision", false,
            [I("PlanId"), N("Revision"), T("SnapshotJson"), U("ReadyAtUtc")]),
        new("periodReviews", "PeriodReviews", "Kind,FirstDate,LastDate,GeneratedAtUtc,Id", false,
            [I("Id"), N("Kind"), Y("FirstDate", "firstDateNewYork"), Y("LastDate", "lastDateNewYork"), U("FromUtc"), U("BeforeUtc"),
             N("ScopeKind"), I("AccountId"), T("AccountDisplayName"), U("GeneratedAtUtc"), T("Provider"), T("Model"),
             T("EvidenceContractVersion"), T("ResponseContractVersion"), T("PacketId"), T("EvidenceJson"), T("ResponseJson"), T("MetadataJson")]),
        new("tradingRulePositions", "TradingRulePositions", "Position,RuleId", false,
            [I("RuleId"), N("InitialRevision"), N("Position")]),
        new("tradingRuleCatalogState", "TradingRuleCatalogState", "Id", false, [N("Id"), N("Version")]),
        new("tradingRuleRevisions", "TradingRuleRevisions", "RuleId,Revision", false,
            [I("RuleId"), N("Revision"), N("ContractVersion"), B("IsGlobal"), N("CheckCapability"), T("Name"), T("Criterion"), T("RequiredEvidence"),
             N("Applicability"), N("ScopeKind"), I("AccountId"), T("AccountNameAtRevision"), N("EvaluationMode"), T("AutomaticCriterionJson"), B("IsActive"), U("RecordedAtUtc")]),
        new("tradingRuleAssessments", "TradingRuleAssessments", "RuleId,TargetKey,Sequence", false,
            [I("Id"), I("RuleId"), N("RuleRevision"), N("Sequence"), T("TargetKey"), N("TargetKind"), I("TradeId"),
             Y("NewYorkDate", "newYorkDate"), N("ScopeKind"), I("AccountId"), T("AccountNameAtAssessment"), T("TargetFingerprint"),
             N("Outcome"), N("Source"), N("Basis"), N("EvidenceSufficiency"), T("Explanation"), T("EvidenceJson"),
             I("SupersedesAssessmentId"), U("RecordedAtUtc"), T("EvaluationJson"), T("EvaluationFingerprint")]),
        new("accounts", "TradingAccounts", "Id", true,
            [I("Id"), T("Name"), N("AccountType"), T("ProviderName"), T("ExternalAccountId"), T("Currency"), D("StartingBalance"), B("IsActive"), U("CreatedAtUtc"), U("UpdatedAtUtc")]),
        new("instruments", "Instruments", "Id", false,
            [I("Id"), T("Symbol"), T("DisplayName"), N("AssetClass"), T("Exchange"), T("Currency"), D("TickSize"), D("TickValue"), B("IsActive"), U("CreatedAtUtc"), U("UpdatedAtUtc")]),
        new("trades", "Trades", "Id", true,
            [I("Id"), I("TradingAccountId"), I("InstrumentId"), D("PricingPointValue"), T("PricingCurrency"), I("TradingSetupId"), U("CreatedAtUtc"), U("UpdatedAtUtc")]),
        new("executions", "TradeExecutions", "TradeId,Sequence,Id", true,
            [I("Id"), I("TradeId"), N("Sequence"), U("ExecutedAtUtc"), N("Side"), D("Quantity"), D("Price"), D("Commission"), D("Fees"), T("ExternalExecutionId"), T("ExternalOrderId"), T("BrokerSymbol")]),
        new("setups", "TradingSetups", "Id", false,
            [I("Id"), T("Name"), T("Description"), B("IsActive"), U("CreatedAtUtc"), U("UpdatedAtUtc")]),
        new("playbookRevisions", "PlaybookRevisions", "TradingSetupId,Revision", false,
            [I("TradingSetupId"), N("Revision"), N("ContractVersion"), T("SetupName"), T("SetupDescription"),
             T("MarketContext"), T("EntryConditions"), T("Confirmations"), T("Invalidation"), T("ExecutionGuidance"), U("RecordedAtUtc")]),
        new("playbookExamples", "PlaybookExamples", "TradingSetupId,Revision,Position", false,
            [I("TradingSetupId"), N("Revision"), I("ExampleId"), N("Position"), T("Annotation"), I("SourceTradeId"), I("SourceScreenshotId"),
             N("Kind"), T("ImageStorageKey"), T("ImageFileName"), N("ImageSizeBytes"), T("ImageSha256")]),
        new("tradePlaybookContexts", "TradePlaybookContexts", "TradeId", false,
            [I("TradeId"), N("ContractVersion"), I("TradingSetupId"), N("Revision"), U("TradeOpenedAtUtc"), U("CapturedAtUtc"), T("DefinitionJson")]),
        new("tradePlaybookAssociations", "TradePlaybookAssociations", "TradeId,Sequence", false,
            [I("TradeId"), N("Sequence"), I("TradingSetupId"), N("Revision"), U("RecordedAtUtc"), U("TradeOpenedAtUtc"), T("DefinitionJson")]),
        new("mistakes", "TradingMistakes", "Id", false,
            [I("Id"), T("Name"), T("Description"), B("IsActive"), U("CreatedAtUtc"), U("UpdatedAtUtc")]),
        new("tradeMistakes", "TradeMistakes", "TradeId,TradingMistakeId,Id", false,
            [I("Id"), I("TradeId"), I("TradingMistakeId"), T("Note"), U("CreatedAtUtc"), U("UpdatedAtUtc")]),
        new("screenshots", "TradeScreenshots", "TradeId,Id", false,
            [I("Id"), I("TradeId"), N("Type"), T("StorageKey"), T("FileName"), U("CapturedAtUtc"), T("Timeframe"), T("Description"), U("CreatedAtUtc"), U("UpdatedAtUtc")]),
        new("journals", "DailyJournals", "TradingDate,Id", true,
            [I("Id"), Y("TradingDate", "tradingDateNewYork"), I("TradingAccountId"), T("Text"), T("WentWell"), T("NeedsImprovement"), T("NextTradingDay"), B("IsDraft"), N("Revision"), U("CreatedAtUtc"), U("UpdatedAtUtc")]),
        new("journalRevisions", "DailyJournalRevisions", "JournalId,Revision", false,
            [I("JournalId"), N("Revision"), T("Text"), T("WentWell"), T("NeedsImprovement"), T("NextTradingDay"), B("IsDraft"), U("SavedAtUtc")]),
        new("analyses", "CoachingAnalyses", "ReviewDate,GeneratedAtUtc,Id", false,
            [I("Id"), Y("ReviewDate", "reviewDateNewYork"), N("ScopeKind"), I("AccountId"), T("AccountDisplayName"), U("GeneratedAtUtc"), T("Provider"), T("Model"), T("EvidenceContractVersion"), T("ResponseContractVersion"), T("PacketId"), T("EvidenceJson"), T("ResponseJson"), T("MetadataJson")]),
        new("tradovateImportExecutions", "TradovateImportedExecutions", "TradeExecutionId", false,
            [I("TradeExecutionId"), I("TradeId"), I("TradingAccountIdAtImport"), T("BrokerSymbol"), N("Side"), T("ExternalExecutionId"), N("AllocationIndex"), D("SourceFillQuantity"), D("AllocatedQuantity"), D("SourceFillPrice"), U("SourceFillExecutedAtUtc"), U("ImportedAtUtc")]),
        new("topstepImportRows", "TopstepImportedRows", "Id", false,
            [I("Id"), I("TradeId"), I("TradingAccountIdAtImport"), T("SourceId"), T("EconomicFingerprint"), T("SourceRowJson"), T("PreviewFingerprint"), T("SourceContentSha256"), T("Representation"), I("DerivedEntryExecutionId"), I("DerivedExitExecutionId"), U("ImportedAtUtc")])
    ];
}
