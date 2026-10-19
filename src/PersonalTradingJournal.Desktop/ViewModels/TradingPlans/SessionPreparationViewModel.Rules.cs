using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.Input;
using PersonalTradingJournal.Application.Rules;
using PersonalTradingJournal.Application.TradingPlans;
using PersonalTradingJournal.Desktop.ViewModels.Rules;
using PersonalTradingJournal.Domain.Rules;
using PersonalTradingJournal.Domain.TradingPlans;

namespace PersonalTradingJournal.Desktop.ViewModels.TradingPlans;

public sealed partial class SessionPreparationViewModel
{
    public const int MaximumRuleChoices = 1000;
    private readonly ObservableCollection<PlanRuleItem> _linkedRules = [];
    private ITradingRuleReader? _rulesReader;
    private ITradingRuleStore? _rulesStore;
    private IReadOnlyList<RuleRow> _ruleChoices = [];
    private RuleRow? _ruleCandidate;
    private Func<Guid, CancellationToken, Task<bool>>? _ruleNavigation;
    public ReadOnlyObservableCollection<PlanRuleItem> LinkedRules { get; private set; } = null!;
    public string LinkedRuleCount => $"{_linkedRules.Count} linked {(_linkedRules.Count == 1 ? "Rule" : "Rules")}";
    public IReadOnlyList<RuleRow> RuleChoices { get => _ruleChoices; private set => SetProperty(ref _ruleChoices, value); }
    public RuleRow? RuleCandidate
    {
        get => _ruleCandidate;
        set { if (CanEdit && (value is null || RuleChoices.Contains(value)) && SetProperty(ref _ruleCandidate, value)) LinkRuleCommand.NotifyCanExecuteChanged(); }
    }
    public IAsyncRelayCommand LoadRulesCommand { get; private set; } = null!;
    public IAsyncRelayCommand LinkRuleCommand { get; private set; } = null!;
    public Func<Guid, CancellationToken, Task<bool>>? RuleNavigation
    { get => _ruleNavigation; set { _ruleNavigation = value; NotifyRuleAccess(); } }

    private void InitializeRules()
    {
        LinkedRules = new(_linkedRules);
        foreach (var link in _original.Rules)
        {
            var row = new PlanRuleItem(this, link); _linkedRules.Add(row);
            if (IsReadOnly)
            {
                var snapshot = (_historical ?? _plan.LatestReadyRevision)!.References.Rules.Single(r => r.RuleId == link.RuleId);
                row.Present(FormatRule(TradingRuleRevision.Rehydrate(snapshot.RuleId, snapshot.Revision, snapshot.Definition, true, snapshot.RecordedAtUtc), "saved · read-only"), false, name: snapshot.Definition.Name);
            }
        }
        LoadRulesCommand = new AsyncRelayCommand(LoadRulesAsync, () => !IsBusy && !_disposed && _rulesReader is not null);
        LinkRuleCommand = new AsyncRelayCommand(LinkRuleAsync, () => CanEdit && RuleCandidate is not null && RuleChoices.Contains(RuleCandidate));
    }
    public void ConfigureRules(ITradingRuleReader reader, ITradingRuleStore store)
    { ArgumentNullException.ThrowIfNull(reader); ArgumentNullException.ThrowIfNull(store); _rulesReader = reader; _rulesStore = store; NotifyRuleAccess(); }
    public async Task LoadRulesAsync(CancellationToken ct = default)
    {
        if (IsBusy || _disposed || _rulesReader is null || _rulesStore is null) return;
        using var operation = Begin(ct);
        try
        {
            // Bounded pages, no automatic retry of a changing catalog and no partial publication.
            var choices = new List<RuleRow>(); long? version = null; int? count = null;
            for (int page = 1; ; page++)
            {
                var result = await Task.Run(() => _rulesReader.ReadCatalogAsync(page, 100, operation.Token), operation.Token);
                operation.Token.ThrowIfCancellationRequested();
                if (result.TotalCount > MaximumRuleChoices) { Report($"Rule selection supports at most {MaximumRuleChoices} eligible Rules. No partial catalog was loaded.", PlanNoticeKind.Warning); return; }
                if (version.HasValue && (version != result.CatalogVersion || count != result.TotalCount))
                { Report("The Rule catalog changed during loading. Reload Rules; input and links are retained.", PlanNoticeKind.Info); return; }
                version = result.CatalogVersion; count = result.TotalCount;
                if (result.Items.Any(r => !r.Revision.IsActive || !r.Revision.Definition.IsGlobal) || result.Items.Count > 100 || result.TotalCount < 0)
                    throw new InvalidDataException("Invalid Rule catalog.");
                choices.AddRange(result.Items.Select(r => new RuleRow(r)));
                if (choices.Count >= result.TotalCount) break;
                if (result.Items.Count == 0 || choices.Count >= MaximumRuleChoices) throw new InvalidDataException("Incomplete Rule catalog.");
            }
            if (choices.Count != count || choices.Select(r => r.Id).Distinct().Count() != choices.Count) throw new InvalidDataException("Invalid Rule page membership.");
            RuleChoices = choices;
            foreach (var row in _linkedRules)
            {
                var link = row.Link;
                var current = await Task.Run(() => _rulesStore.GetLatestAsync(link.RuleId, operation.Token), operation.Token);
                bool available = current is { IsActive: true, Definition.IsGlobal: true };
                string status = current is null ? "Current Rule unavailable; saved context remains readable." :
                    !available ? "Current Rule deleted/archived or legacy scoped; new links are not permitted." :
                    current.Revision != link.ExpectedRevision ? "Current Rule changed. Select its intended revision, then explicitly refresh references and reconfirm." : "Selected Rule is current.";
                if (IsReadOnly)
                {
                    var frozen = (_historical ?? _plan.LatestReadyRevision)!.References.Rules.Single(r => r.RuleId == link.RuleId);
                    row.Present(FormatRule(TradingRuleRevision.Rehydrate(frozen.RuleId, frozen.Revision, frozen.Definition, true, frozen.RecordedAtUtc), "saved · read-only") +
                        "\n\n" + (available && current!.Revision != link.ExpectedRevision ? "Current Rule changed. The saved definition remains unchanged; start a Draft to change preparation." : status), available,
                        available && current!.Revision != link.ExpectedRevision ? "Current Rule changed; saved context is unchanged." : status,
                        frozen.Definition.Name, !available || current!.Revision != link.ExpectedRevision);
                }
                else
                {
                    var exact = await Task.Run(() => _rulesReader.ReadRevisionsAsync(link.RuleId,
                        link.ExpectedRevision == int.MaxValue ? null : link.ExpectedRevision + 1, 1, operation.Token), operation.Token);
                    var selected = exact.SingleOrDefault(r => r.Revision.Revision == link.ExpectedRevision && r.Revision.RuleId == link.RuleId);
                    row.Present(selected is null ? "Selected Rule definition unavailable. Remove/relink explicitly; input is retained.\n" + status :
                        FormatRule(selected.Revision, "selected") + "\n\n" + status, available,
                        selected is null ? "Selected Rule definition unavailable. Remove/relink explicitly; input is retained." : status,
                        selected?.Revision.Definition.Name, !available || current!.Revision != link.ExpectedRevision || selected is null);
                    if (!available || current!.Revision != link.ExpectedRevision || selected is null) ObserveWriteFailure(TradingPlanWriteStatus.ReferenceChanged, invalidateCurrency: false);
                }
                operation.Token.ThrowIfCancellationRequested();
            }
        }
        catch (OperationCanceledException) { Report("Rule loading cancelled. Links and entered input are retained.", PlanNoticeKind.Info); }
        catch (Exception) { Report("Rules could not be loaded. Retry loading; links and entered input are retained.", PlanNoticeKind.Error); }
        finally { End(); }
    }
    public async Task LinkRuleAsync(CancellationToken ct = default)
    {
        if (!CanEdit || RuleCandidate is not { } candidate || !RuleChoices.Contains(candidate) || _rulesStore is null) return;
        using var operation = Begin(ct);
        try
        {
            var current = await Task.Run(() => _rulesStore.GetLatestAsync(candidate.Id, operation.Token), operation.Token);
            operation.Token.ThrowIfCancellationRequested();
            if (current is not { IsActive: true, Definition.IsGlobal: true } || current.RuleId != candidate.Id || current.Revision != candidate.Details.Revision.Revision)
            { Report("The selected Rule changed or is unavailable. Reload and review the intended revision; existing links are retained.", PlanNoticeKind.Error); return; }
            var existing = _linkedRules.SingleOrDefault(r => r.Link.RuleId == current.RuleId);
            var link = new TradingPlanRuleLink(current.RuleId, current.Revision);
            if (existing?.Link == link) { Report("This exact Rule revision is already linked.", PlanNoticeKind.Info); return; }
            if (existing is null && _linkedRules.Count >= TradingPlanContent.MaximumRules)
            { Report($"Link at most {TradingPlanContent.MaximumRules} Rules.", PlanNoticeKind.Warning); return; }
            if (existing is null) { existing = new(this, link); _linkedRules.Add(existing); }
            else existing.Replace(link);
            existing.Present(FormatRule(current, "selected"), true, name: current.Definition.Name); Changed();
            Report("Rule revision selected for the Draft. Save rechecks references; a link does not assert compliance.", PlanNoticeKind.Info);
        }
        catch (OperationCanceledException) { Report("Rule selection cancelled. Existing links and input are retained.", PlanNoticeKind.Info); }
        catch (Exception) { Report("Rule selection failed. Existing links and input are retained.", PlanNoticeKind.Error); }
        finally { End(); }
    }
    internal void RemoveRule(PlanRuleItem row)
    { if (CanEdit && _linkedRules.Remove(row)) Changed(); }
    internal async Task RequestRuleAsync(PlanRuleItem row, CancellationToken ct)
    {
        if (IsBusy || _disposed || !_linkedRules.Contains(row) || RuleNavigation is not { } request) return;
        try { if (!await request(row.Link.RuleId, ct)) Report("Rule navigation was declined or the source is unavailable. Input is retained.", PlanNoticeKind.Error); }
        catch (OperationCanceledException) { Report("Rule navigation cancelled. Input is retained.", PlanNoticeKind.Info); }
        catch (Exception) { Report("The current Rule could not be opened. Input and saved context are retained.", PlanNoticeKind.Error); }
    }
    private void NotifyRuleAccess()
    {
        LoadRulesCommand?.NotifyCanExecuteChanged(); LinkRuleCommand?.NotifyCanExecuteChanged();
        foreach (var row in _linkedRules) row.NotifyAccess();
        OnPropertyChanged(nameof(LinkedRuleCount));
    }
    private static string FormatRule(TradingRuleRevision r, string state)
    {
        var display = new RuleRow(new(r, null, true));
        return $"{r.Definition.Name} · {state} revision {r.Revision}" +
            (string.IsNullOrWhiteSpace(r.Definition.Criterion) ? "" : "\n" + r.Definition.Criterion) +
            (string.IsNullOrWhiteSpace(r.Definition.RequiredEvidence) ? "" : "\nEvidence guidance: " + r.Definition.RequiredEvidence) +
            (r.Definition.AutomaticCriterion is null ? "" : "\n" + display.AutomaticCondition) +
            "\nPreparation link only; no compliance assessment or automatic check is performed here.";
    }
}
