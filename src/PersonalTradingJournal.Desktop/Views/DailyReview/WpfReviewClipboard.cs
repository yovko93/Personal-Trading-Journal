using System.Windows;
using PersonalTradingJournal.Desktop.ViewModels.DailyReview;

namespace PersonalTradingJournal.Desktop.Views.DailyReview;

public sealed class WpfReviewClipboard : IReviewClipboard
{
    public void Copy(string text) => Clipboard.SetText(text);
    public string Paste() => Clipboard.GetText();
}
