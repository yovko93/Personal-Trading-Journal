using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.Input;
using PersonalTradingJournal.Application.TradingPlans;
using PersonalTradingJournal.Domain.TradingPlans;

namespace PersonalTradingJournal.Desktop.ViewModels.TradingPlans;

public sealed partial class SessionPreparationViewModel
{
    private TradingPlanChecklist _pendingConfirmed;
    private Dictionary<TradingPlanEditArea, string> _editStates = [];
    private ITradingPlanReferenceReader? _referenceReader;
    private string? _verifiedSelection, _currency;
    private bool _currencyVerified, _refreshRequired, _writeConflict, _saveFailed;
    private string _referenceError = "Check references or save the Draft to verify the current sources.";
    private TradingPlanAccountSnapshot? _currentAccount;
    public bool IsDisposed => _disposed;
    private string _planCurrency = "";
    public bool IsGlobalScope => Key.Scope == TradingPlanScope.AllAccounts;
    public string PlanCurrency { get => _planCurrency; set { if (CanEdit && SetProperty(ref _planCurrency, value)) { OnPropertyChanged(nameof(CurrencyError)); Changed(); } } }
    public PreparationField RiskPerTrade { get; private set; } = null!;
    public PreparationField DailyLossLimit { get; private set; } = null!;
    public PreparationField MaximumTrades { get; private set; } = null!;
    public PreparationField ConsecutiveLosses { get; private set; } = null!;
    public IReadOnlyList<PreparationField> RiskFields { get; private set; } = [];
    public IReadOnlyList<PlanChecklistItem> Checklist { get; private set; } = [];
    public IAsyncRelayCommand CheckReferencesCommand { get; private set; } = null!;
    public bool CurrencyVerified => IsReadOnly || _currencyVerified;
    public string CurrencyLabel => IsGlobalScope ? $"Trader-wide plan currency: {PlanCurrency} · not a per-Account budget" : _currency is null ? "Account currency unverified" :
        CurrencyVerified ? $"Account currency: {_currency}" : $"Last verified currency: {_currency} · recheck required";
    public string CurrencyMessage => IsReadOnly ? "Saved currency and limits from this Ready revision." :
        IsGlobalScope ? "Optional trader-wide planned amounts in the explicit plan currency. Blank is unset; zero permits none. No FX conversion or per-Account multiplication." :
        !CurrencyVerified && _currentAccount is { } account ? $"Current Account currency: {account.Currency}. Review the amounts, then explicitly refresh references to accept this context. Values are not converted." :
        "Optional planned amounts in this exact Account's currency. Blank is unset; zero permits none. No broker enforcement or loss monitoring.";
    public bool HasMonetaryLimits => !string.IsNullOrWhiteSpace(RiskPerTrade.Text) || !string.IsNullOrWhiteSpace(DailyLossLimit.Text);
    public bool CanConfirm => CanEdit && !HasValidationErrors && !_refreshRequired && !_writeConflict;
    public int CompletedConfirmations => TradingPlanChecklistPolicy.CompletedCount(Confirmed);
    public int ConfirmationProgress => CompletedConfirmations * 20;
    public string ConfirmationSummary => $"{CompletedConfirmations} of 5 confirmed";
    public bool RequiresReferenceRefresh => _refreshRequired;
    public bool HasVerifiedReferences => IsReadOnly || _verifiedSelection == ReferenceSelection();
    public IReadOnlyList<PlanReadinessBlocker> ReadinessBlockers
    {
        get
        {
            if (IsReadOnly) return [];
            var blockers = new List<PlanReadinessBlocker>();
            if (CurrencyError.Length > 0) blockers.Add(new("Currency", CurrencyError));
            if (IsBusy) blockers.Add(new("Operation", "Wait for the current operation to finish."));
            if (_writeConflict) blockers.Add(new("Conflict", "This Plan changed elsewhere. Keep your input and reload/resolve the newer Draft before continuing."));
            if (_saveFailed) blockers.Add(new("Save", "The last save could not be verified. Retry saving; entered preparation is retained."));
            blockers.AddRange(Fields.Where(f => f.IsInvalid).Select(f => new PlanReadinessBlocker(f.Label, $"{f.Label}: {f.Error}")));
            if (TryBuildContent(out var content))
                blockers.AddRange(TradingPlanReadiness.Validate(content!).Select(i => new PlanReadinessBlocker(i.Field, i.Message)));
            if (!HasVerifiedReferences || _refreshRequired) blockers.Add(new("References", _referenceError));
            if (IsDirty) blockers.Add(new("Draft", "Save the Draft to verify and persist the current preparation and explicit confirmations."));
            return blockers;
        }
    }
    public bool IsPreparationComplete => !IsReadOnly && ReadinessBlockers.Count == 0;
    public string ReadinessSummary => IsReadOnly ? "Saved Ready revision · confirmations and limits are read-only." :
        _writeConflict ? "Plan conflict — resolve the newer Draft. Entered preparation is retained." :
        HasValidationErrors ? "Malformed input — correct the highlighted fields. All entered text is retained." :
        _refreshRequired ? "References require review and an explicit refresh/relink before reconfirming preparation." :
        IsPreparationComplete ? "Preparation complete and saved. Mark Ready must recheck sources and concurrency." :
        "Preparation incomplete or awaiting verification. Incomplete valid content can still be saved as Draft.";

    private void InitializeRiskReadiness()
    {
        _planCurrency = _original.PlanCurrency ?? "";
        _pendingConfirmed = _original.Confirmed;
        var limits = _original.RiskLimits;
        PreparationField Amount(string label, decimal? value) => new(label, value?.ToString(_culture) ?? "", 128, true, _culture, () => CanEdit, Changed, minimum: 0, numericNoun: "amount");
        PreparationField Count(string label, int? value, int minimum) => new(label, value?.ToString(_culture) ?? "", 128, true, _culture, () => CanEdit, Changed, minimum, integer: true);
        RiskPerTrade = Amount("Risk per trade", limits.RiskPerTrade); DailyLossLimit = Amount("Daily loss limit", limits.DailyLossLimit);
        MaximumTrades = Count("Maximum number of trades", limits.MaximumTrades, 0);
        ConsecutiveLosses = Count("Stop after consecutive losses", limits.StopAfterConsecutiveLosses, 1);
        RiskFields = [RiskPerTrade, DailyLossLimit, MaximumTrades, ConsecutiveLosses];
        Checklist = [new(this, TradingPlanChecklist.ContextAndLevels, "Context and levels", "I reviewed the context and available levels."),
            new(this, TradingPlanChecklist.ScenariosOrStandAside, "Scenarios or Stand Aside", "I reviewed the conditional scenarios or intentional no-trade decision."),
            new(this, TradingPlanChecklist.RiskLimits, "Risk limits", "I reviewed the planned limits, including any unset values."),
            new(this, TradingPlanChecklist.News, "News calendar", "I reviewed scheduled news manually; no calendar feed is connected."),
            new(this, TradingPlanChecklist.MindsetAndFocus, "Mindset and focus", "I reviewed today's behavioral focus.")];
        InitializeRules();
        if (IsReadOnly) { _currency = (_historical ?? _plan.LatestReadyRevision)!.References.Account.Currency; _currencyVerified = true; }
        _editStates = EditStates();
        CheckReferencesCommand = new AsyncRelayCommand(CheckReferencesAsync, () => !IsBusy && !IsReadOnly && !_disposed && _referenceReader is not null);
    }
    public void ConfigureReferenceReader(ITradingPlanReferenceReader reader)
    { ArgumentNullException.ThrowIfNull(reader); _referenceReader = reader; NotifyRiskAccess(); }
    internal void SetConfirmation(TradingPlanChecklist flag, bool value)
    {
        if (Checklist.Single(c => c.Flag == flag).CanInteract)
        {
            var next = value ? _pendingConfirmed | flag : _pendingConfirmed & ~flag;
            if (next != _pendingConfirmed) { _pendingConfirmed = next; Changed(); }
        }
        foreach (var check in Checklist) check.NotifyState();
    }
    private TradingPlanRiskLimits BuildRisk()
    {
        RiskPerTrade.TryGetDecimal(out var perTrade); DailyLossLimit.TryGetDecimal(out var daily);
        MaximumTrades.TryGetInteger(out var maximum); ConsecutiveLosses.TryGetInteger(out var consecutive);
        return new(perTrade, daily, maximum, consecutive);
    }
    private Dictionary<TradingPlanEditArea, string> EditStates()
    {
        static string Json(object? value) => JsonSerializer.Serialize(value);
        return new()
        {
            [TradingPlanEditArea.Session] = Json(Session), [TradingPlanEditArea.Focus] = Focus.Text,
            [TradingPlanEditArea.Instruments] = Json(_contexts.Select(c => c.InstrumentId).Order()),
            [TradingPlanEditArea.ContextAndLevels] = Json(new { Contexts = _contexts.OrderBy(c => c.InstrumentId).Select(c => new { c.InstrumentId, c.Bias, Thesis = c.Thesis.Text, Levels = c.Levels.Select(f => f.SemanticInput) }), Screenshots = _screenshots.Select(s => s.Id) }),
            [TradingPlanEditArea.ScenariosAndSetups] = Json(_scenarios.Select(s => s.RawState)),
            [TradingPlanEditArea.StandAside] = Json(new { Intent, StandAside = StandAside.Text }),
            [TradingPlanEditArea.News] = News.Text,
            [TradingPlanEditArea.RiskLimitsAndRules] = Json(new { PlanCurrency, Limits = RiskFields.Select(f => f.SemanticInput), Rules = _linkedRules.Select(r => r.Link) })
        };
    }
    private void InvalidateEditedConfirmations()
    {
        if (IsReadOnly) return;
        var next = EditStates();
        foreach (var (area, state) in next)
            if (_editStates.TryGetValue(area, out var previous) && previous != state)
                _pendingConfirmed = TradingPlanChecklistPolicy.AfterEdit(_pendingConfirmed, area);
        _editStates = next;
    }
    private string ReferenceSelection() => JsonSerializer.Serialize(new { Key.AccountId, PlanCurrency,
        Instruments = _contexts.Select(c => c.InstrumentId).Order(), Setups = _scenarios.Select(s => s.Setup).Where(s => s is not null).Distinct().OrderBy(s => s!.SetupId),
        Rules = _linkedRules.Select(r => r.Link).OrderBy(r => r.RuleId) });

    public async Task CheckReferencesAsync(CancellationToken ct = default)
    {
        if (!CanEdit || _referenceReader is null) return;
        if (!TryBuildContent(out var content))
        { Report("Correct malformed input before checking references. All input is retained.", PlanNoticeKind.Warning); ValidationAttempt++; OnPropertyChanged(nameof(ValidationAttempt)); return; }
        using var operation = Begin(ct);
        try
        {
            var expected = Token;
            var result = await Task.Run(() => _referenceReader.CheckAsync(Key, expected, content!, operation.Token), operation.Token);
            operation.Token.ThrowIfCancellationRequested();
            if (result.Key != Key || result.Expected != expected) throw new InvalidDataException("Mismatched reference check.");
            _currentAccount = result.CurrentAccount;
            _currencyVerified = result.AccountMatchesBaseline && result.CurrentAccount?.AccountId == Key.AccountId;
            if (_currencyVerified) _currency = result.CurrentAccount!.Currency;
            if (result.Status == TradingPlanWriteStatus.Success && result.VerifiedReferences is { } refs && _currencyVerified)
            {
                // A read cannot acknowledge a prior changed-reference result or clear a conflict.
                if (!_refreshRequired && !_writeConflict) AcceptReferences(refs);
                Report(_refreshRequired ? "References still require an explicit refresh and reconfirmation." : "References checked. Mark Ready will recheck them at commit.",
                    _refreshRequired ? PlanNoticeKind.Warning : PlanNoticeKind.Info);
            }
            else
            {
                _verifiedSelection = null; ObserveWriteFailure(result.Status, !result.AccountMatchesBaseline);
                _referenceError = result.Status == TradingPlanWriteStatus.Conflict ? "The stored Plan changed. Resolve the newer Draft without losing this input." :
                    "A reference changed or is unavailable. Review linked sources and the Account currency, then explicitly refresh/relink and reconfirm.";
                Report(_referenceError, PlanNotice.ForWrite(result.Status));
            }
        }
        catch (OperationCanceledException) { Report("Reference checking cancelled. Input and confirmations are retained.", PlanNoticeKind.Warning); }
        catch (Exception) { _verifiedSelection = null; _referenceError = "References could not be verified. Retry the check; input is retained."; Report(_referenceError, PlanNoticeKind.Error); }
        finally { End(); }
    }
    private void AcceptReferences(TradingPlanReferenceSnapshot refs)
    {
        if (refs.Account.AccountId != Key.AccountId) { _verifiedSelection = null; _referenceError = "Account context could not be verified."; return; }
        _currency = refs.Account.Currency; _currentAccount = refs.Account; _currencyVerified = true;
        _verifiedSelection = ReferenceSelection(); _referenceError = "";
    }
    private void AcceptSavedReadiness(TradingPlanWriteResult result)
    {
        _pendingConfirmed = _original.Confirmed; _editStates = EditStates();
        _writeConflict = false; _saveFailed = false; _refreshRequired = false;
        _verifiedSelection = null;
        if (result.VerifiedReferences is { } refs) AcceptReferences(refs);
        else { _currencyVerified = false; _referenceError = "Check references to verify this saved Draft's current context."; }
    }
    private void ObserveWriteFailure(TradingPlanWriteStatus status, bool invalidateCurrency = true)
    {
        if (status == TradingPlanWriteStatus.Conflict) _writeConflict = true;
        if (status == TradingPlanWriteStatus.StorageFailure) _saveFailed = true;
        if (status is TradingPlanWriteStatus.ReferenceChanged or TradingPlanWriteStatus.ReferenceUnavailable)
        { _refreshRequired = true; _verifiedSelection = null; if (invalidateCurrency) _currencyVerified = false; _referenceError = "Review changed/unavailable references, explicitly refresh or relink, and reconfirm preparation."; }
    }
    private void NotifyRiskAccess()
    {
        CheckReferencesCommand?.NotifyCanExecuteChanged(); NotifyRuleAccess();
        foreach (var check in Checklist) check.NotifyState();
        foreach (string name in new[] { nameof(CurrencyLabel), nameof(CurrencyMessage), nameof(CurrencyVerified), nameof(CanConfirm), nameof(HasMonetaryLimits), nameof(Confirmed),
            nameof(CompletedConfirmations), nameof(ConfirmationProgress), nameof(ConfirmationSummary), nameof(ReadinessBlockers), nameof(ReadinessSummary), nameof(IsPreparationComplete),
            nameof(HasVerifiedReferences), nameof(RequiresReferenceRefresh) }) OnPropertyChanged(name);
    }
}
