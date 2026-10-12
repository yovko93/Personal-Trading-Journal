namespace PersonalTradingJournal.Application.DailyReview;

public enum ReviewPlaybookState { NoAssociation, LegacyClassification, Captured }
public enum ReviewPlaybookAssociationState { Current, Replaced, Removed }

/// <summary>Text-only projection. Deliberately has no image storage key, filename, hash or bytes.
/// Example labels and annotations are user observations; the provider has not inspected images.</summary>
public sealed record ReviewPlaybookExample(Guid Id, string Annotation, string Kind,
    Guid? SourceTradeId, Guid? SourceScreenshotId, bool HasImage);

public sealed record ReviewPlaybookGuidance(Guid SetupId, string SetupName, string? SetupDescription,
    int Revision, DateTimeOffset RevisionRecordedAtUtc, string MarketContext, string EntryConditions,
    string Confirmations, string Invalidation, string ExecutionGuidance, IReadOnlyList<ReviewPlaybookExample> Examples);

/// <summary>Sequence zero identifies the original M17.2 capture; positive sequences identify
/// immutable M17.5 transitions. Null guidance explicitly means no captured revision, not empty guidance.</summary>
public sealed record ReviewPlaybookAssociation(int Sequence, Guid? SetupId, DateTimeOffset CapturedAtUtc,
    DateTimeOffset TradeOpenedAtUtc, ReviewPlaybookAssociationState State, ReviewPlaybookGuidance? Guidance);

/// <summary>All retained transitions as observed in the same read transaction as the Trade.
/// Association, including a retrospective capture, is not verified adherence.</summary>
public sealed record DailyReviewPlaybookEvidence(ReviewPlaybookState CurrentState,
    IReadOnlyList<ReviewPlaybookAssociation> Associations);
