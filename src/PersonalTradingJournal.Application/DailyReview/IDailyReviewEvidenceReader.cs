namespace PersonalTradingJournal.Application.DailyReview;

/// <summary>Fresh, consistent, read-only evidence; no statistics or AI interpretation.</summary>
public interface IDailyReviewEvidenceReader
{
    Task<DailyReviewEvidence> GetAsync(DailyReviewQuery query, CancellationToken cancellationToken = default);
}
