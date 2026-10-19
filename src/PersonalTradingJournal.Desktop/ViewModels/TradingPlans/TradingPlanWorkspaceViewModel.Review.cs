using CommunityToolkit.Mvvm.Input;
using PersonalTradingJournal.Application.TradingPlans;
using PersonalTradingJournal.Domain.TradingPlans;

namespace PersonalTradingJournal.Desktop.ViewModels.TradingPlans;
public sealed partial class TradingPlanWorkspaceViewModel
{
    public Func<TradingPlanKey, string, Task>? OpenDailyReview { get; set; }
    private Action? _returnToReview;
    public Action? ReturnToReview { get => _returnToReview; set { _returnToReview = value; OnPropertyChanged(nameof(CanReturnToReview)); } }
    public bool CanReturnToReview => ReturnToReview is not null;
    public IAsyncRelayCommand OpenDailyReviewCommand { get; private set; } = null!;
    public IRelayCommand ReturnToReviewCommand { get; private set; } = null!;
    private void InitializeReviewNavigation()
    {
        OpenDailyReviewCommand = new AsyncRelayCommand(async () =>
        {
            if (IsBusy || OpenDailyReview is null) return;
            var key = IsHistorySelected ? _viewedRevision?.Key : Editor?.Key;
            if (key is null) { Report("Open saved preparation or a Session Brief first.", PlanNoticeKind.Info); return; }
            string label = IsHistorySelected ? _viewedRevision!.References.Account.Name : _account?.Name ?? "Original Account";
            await OpenDailyReview(key, label);
        });
        ReturnToReviewCommand = new RelayCommand(() => { if (!IsBusy) ReturnToReview?.Invoke(); });
    }

    public async Task<bool> OpenReviewRevisionAsync(ReviewPlanRevision reference)
    {
        if (!CanUseHistory || IsBusy) return false;
        bool opened = false;
        await HistoryReadAsync(async (ct, owns) =>
        {
            var plan = await Task.Run(() => _store.LoadAsync(reference.Key, ct), ct);
            var revision = await Task.Run(() => _store.LoadReadyAsync(reference.Id, ct), ct);
            if (plan?.Id != reference.Id.PlanId || revision?.Id != reference.Id || revision.Key != reference.Key || revision.ReadyAtUtc != reference.ReadyAtUtc)
            { if (owns()) HistoryMessage = "The exact Ready source is unavailable. Its saved Review snapshot remains readable; no replacement Plan was opened."; return; }
            if (!owns()) return;
            ClearHistorical(); _historyPlan = plan; SelectedHistoryRow = new(TradingPlanLoadedState.FromPlan(plan));
            RevisionRows = [new(revision)]; _revisionPage = 0; HasOlderRevisions = false;
            _revisionCursors.Clear(); _revisionCursors.Add(null);
            _viewedRevision = revision; HistoricalViewer = CreateEditor(plan, revision); SelectTab(true);
            await HistoricalViewer.LoadThumbnailsAsync(ct);
            if (!owns()) return;
            HistoryMessage = "Exact Ready revision from Review. Its actual timestamp determines entry-time eligibility; later preparation is retrospective. Session Brief input is retained.";
            opened = true;
        });
        return opened;
    }
}
