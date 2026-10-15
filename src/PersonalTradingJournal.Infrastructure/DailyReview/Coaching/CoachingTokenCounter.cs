using Microsoft.ML.Tokenizers;

namespace PersonalTradingJournal.Infrastructure.DailyReview.Coaching;

/// <summary>Bundled vocabulary, no download. Local estimate, not billed usage or provider framing.</summary>
internal static class CoachingTokenCounter
{
    private static readonly Lazy<TiktokenTokenizer> Tokenizer = new(() => TiktokenTokenizer.CreateForEncoding("o200k_base"));
    internal static int Count(string text) => Tokenizer.Value.CountTokens(text);
}
