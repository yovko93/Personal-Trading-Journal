using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Analytics;
using PersonalTradingJournal.Application.Analytics.Breakdown;
using PersonalTradingJournal.Application.Common.Time;
using PersonalTradingJournal.Application.Instruments;
using PersonalTradingJournal.Desktop.ViewModels.Dashboard;
using PersonalTradingJournal.Desktop.Navigation;
using PersonalTradingJournal.Application.Trades;

namespace PersonalTradingJournal.Desktop.ViewModels.Breakdown;

public sealed record BreakdownReferenceOption(Guid? Id, string Name, bool Available = true);
public sealed record BreakdownChoice<T>(T Value, string Name);

/// <summary>Session-local draft filters and one atomically published, query-owned result.</summary>
public sealed class BreakdownViewModel : ObservableObject, IDisposable
{
    private readonly IBreakdownAnalyticsReader _reader;
    private readonly ITradingAccountReader _accounts;
    private readonly IInstrumentReader _instruments;
    private readonly TimeProvider _time;
    private CancellationTokenSource? _cancellation;
    private long _generation, _draftVersion;
    private bool _active, _disposed, _started, _loading, _stale;
    private DateOnly _anchor;
    private DashboardPeriod _period;
    private BreakdownGrouping _grouping = BreakdownGrouping.Instrument;
    private BreakdownCalculationMode _mode = BreakdownCalculationMode.NetPreferred;
    private string _currency = "USD", _start = "", _end = "", _defaultCurrency = "USD";
    private BreakdownReferenceOption _account = new(null, "All accounts"), _instrument = new(null, "All instruments");
    private IReadOnlyList<BreakdownReferenceOption> _accountOptions = [], _instrumentOptions = [];
    private IReadOnlyList<string> _currencies = [];
    private BreakdownAnalyticsSnapshot? _result;
    private BreakdownPresentation? _presentation;
    private IReadOnlyList<BreakdownGroupPresentation> _groupRows = [];
    private BreakdownGroupKey? _selectedGroupKey;
    private BreakdownSortColumn? _sortColumn;
    private bool _sortDescending;
    private BreakdownChartMetric _chartMetric;
    private BreakdownChartPresentation? _chart;
    private BreakdownQuery? _submitted;
    private string _status = "Apply filters to load Breakdown.";
    private string? _error, _dateError, _currencyError;
    private string? _resultAccount, _resultInstrument;

    public BreakdownViewModel(IBreakdownAnalyticsReader reader, ITradingAccountReader accounts,
        IInstrumentReader instruments, TimeProvider time)
    {
        _reader = reader; _accounts = accounts; _instruments = instruments; _time = time;
        _anchor = Today;
        _accountOptions = [_account]; _instrumentOptions = [_instrument];
        ApplyCommand = new AsyncRelayCommand(ApplyAsync, AsyncRelayCommandOptions.AllowConcurrentExecutions);
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, AsyncRelayCommandOptions.AllowConcurrentExecutions);
        ResetCommand = new AsyncRelayCommand(ResetAsync, AsyncRelayCommandOptions.AllowConcurrentExecutions);
        CancelCommand = new RelayCommand(Cancel, () => IsLoading);
        ViewTradesCommand = new AsyncRelayCommand<BreakdownGroupPresentation>(ViewTradesAsync, CanViewTrades);
    }

    public IReadOnlyList<BreakdownChoice<DashboardPeriod>> Periods { get; } =
        [new(DashboardPeriod.All, "All history"), new(DashboardPeriod.Week, "Current week"),
         new(DashboardPeriod.Month, "Current month"), new(DashboardPeriod.Year, "Current year"), new(DashboardPeriod.Custom, "Custom dates")];
    public IReadOnlyList<BreakdownChoice<BreakdownGrouping>> Groupings { get; } =
        [new(BreakdownGrouping.TradingSetup, "Setup"), new(BreakdownGrouping.Instrument, "Instrument"),
         new(BreakdownGrouping.Direction, "Direction"), new(BreakdownGrouping.EntryWeekday, "Weekday"),
         new(BreakdownGrouping.EntryHour, "Entry hour"), new(BreakdownGrouping.Account, "Account"),
         new(BreakdownGrouping.HoldingDuration, "Hold time")];
    public IReadOnlyList<BreakdownChoice<BreakdownCalculationMode>> Modes { get; } =
        [new(BreakdownCalculationMode.NetPreferred, "Net preferred"), new(BreakdownCalculationMode.Gross, "Gross")];
    public IReadOnlyList<BreakdownReferenceOption> Accounts { get => _accountOptions; private set => SetProperty(ref _accountOptions, value); }
    public IReadOnlyList<BreakdownReferenceOption> Instruments { get => _instrumentOptions; private set => SetProperty(ref _instrumentOptions, value); }
    public IReadOnlyList<string> Currencies { get => _currencies; private set { SetProperty(ref _currencies, value); OnPropertyChanged(nameof(CurrencySuggestions)); } }
    public string CurrencySuggestions => $"Catalog suggestions: {string.Join(", ", Currencies)}. You may type another historical pricing currency. No currency conversion.";
    public BreakdownReferenceOption SelectedAccount { get => _account; set { if (value is not null && SetProperty(ref _account, value)) Edited(); } }
    public BreakdownReferenceOption SelectedInstrument { get => _instrument; set { if (value is not null && SetProperty(ref _instrument, value)) Edited(); } }
    public DashboardPeriod Period { get => _period; set { if (SetProperty(ref _period, value)) { _anchor = Today; Edited(); OnPropertyChanged(nameof(IsCustom)); } } }
    public bool IsCustom => Period == DashboardPeriod.Custom;
    public string StartDateText { get => _start; set { if (SetProperty(ref _start, value)) Edited(); } }
    public string EndDateText { get => _end; set { if (SetProperty(ref _end, value)) Edited(); } }
    public string Currency { get => _currency; set { if (value is not null && SetProperty(ref _currency, value)) Edited(); } }
    public BreakdownGrouping Grouping { get => _grouping; set { if (SetProperty(ref _grouping, value)) Edited(); } }
    public BreakdownCalculationMode Mode { get => _mode; set { if (SetProperty(ref _mode, value)) Edited(); } }
    public bool IsLoading { get => _loading; private set { SetProperty(ref _loading, value); CancelCommand.NotifyCanExecuteChanged(); } }
    public bool IsStale { get => _stale; private set => SetProperty(ref _stale, value); }
    public string StatusMessage { get => _status; private set => SetProperty(ref _status, value); }
    public string? ErrorMessage { get => _error; private set => SetProperty(ref _error, value); }
    public string? DateValidation { get => _dateError; private set => SetProperty(ref _dateError, value); }
    public string? CurrencyValidation { get => _currencyError; private set => SetProperty(ref _currencyError, value); }
    public BreakdownAnalyticsSnapshot? Result => _result;
    public Func<BreakdownTradeContext, Task>? OpenTradesAsync { get; set; }
    private bool _openingTrades;
    public IAsyncRelayCommand<BreakdownGroupPresentation> ViewTradesCommand { get; }
    private bool CanViewTrades(BreakdownGroupPresentation? row) => _active && !_disposed && !_openingTrades &&
        OpenTradesAsync is not null && row is not null && _groupRows.Any(r => ReferenceEquals(r, row)) && row.Source.Metrics.TradeIds.Count > 0;
    private async Task ViewTradesAsync(BreakdownGroupPresentation? row)
    {
        if (!CanViewTrades(row) || _result is not { } result || OpenTradesAsync is not { } open) return;
        var context = new BreakdownTradeContext(Guid.NewGuid(), new(result.Query, row!.Key), row.Label,
            AppliedContext, row.Metrics.Basis, new TradeIdCohort(row.Source.Metrics.TradeIds));
        _openingTrades = true;
        ViewTradesCommand.NotifyCanExecuteChanged();
        try { await open(context); }
        catch (Exception) { if (_active && !_disposed) ErrorMessage = "The contributing Trades could not be opened. Try View trades again."; }
        finally { _openingTrades = false; ViewTradesCommand.NotifyCanExecuteChanged(); }
    }
    public Task RefreshAppliedAsync() => !_active || _disposed ? Task.CompletedTask
        : _result is null ? RefreshAsync() : LoadTask = LoadAsync(_result.Query);
    public BreakdownPresentation? Presentation => _presentation;
    public IReadOnlyList<BreakdownGroupPresentation> GroupRows => _groupRows;
    public BreakdownGroupKey? SelectedGroupKey => _selectedGroupKey;
    public BreakdownGroupPresentation? SelectedGroup
    {
        get => _groupRows.FirstOrDefault(r => r.Key == _selectedGroupKey);
        set
        {
            // ItemsSource replacement can temporarily clear native selection. Identity is owned here.
            if (value is null || !_groupRows.Any(r => ReferenceEquals(r, value))) return;
            _selectedGroupKey = value.Key;
            OnPropertyChanged(); OnPropertyChanged(nameof(SelectedGroupKey));
            NotifySelection();
        }
    }
    public BreakdownSortColumn? SortColumn => _sortColumn;
    public bool SortDescending => _sortDescending;
    public IReadOnlyList<BreakdownChoice<BreakdownChartMetric>> ChartMetrics { get; } =
        [new(BreakdownChartMetric.Pnl, "Selected P&L"), new(BreakdownChartMetric.Trades, "Eligible Trades"), new(BreakdownChartMetric.WinRate, "Win Rate")];
    public BreakdownChartMetric ChartMetric
    {
        get => _chartMetric;
        set { if (Enum.IsDefined(value) && SetProperty(ref _chartMetric, value)) { RebuildChart(); OnPropertyChanged(nameof(Chart)); OnPropertyChanged(nameof(ChartHeading)); NotifySelection(); } }
    }
    public BreakdownChartPresentation? Chart => _chart;
    public BreakdownChartRow? SelectedChartRow
    {
        get => _chart?.Rows.FirstOrDefault(r => r.Group.Key == _selectedGroupKey);
        set { if (value is not null && _chart?.Rows.Any(r => ReferenceEquals(r, value)) == true) SelectedGroup = value.Group; }
    }
    public BreakdownGroupDetails? SelectedDetails => SelectedGroup is { } group && _result is { } result
        ? new(group, $"{Groupings.First(g => g.Value == result.Query.Grouping).Name} · {result.Query.Currency} · {Modes.First(m => m.Value == result.Query.Mode).Name} · {group.Metrics.Basis}") : null;
    public string SelectionPrompt => SelectedGroup is null ? "Select a group in the chart or table to inspect its calculated results." : "";
    private void RebuildChart() => _chart = _presentation is null ? null : new(_groupRows, _chartMetric, _presentation.Source.Query.Currency, _presentation.Summary.Basis);
    private void NotifySelection()
    {
        OnPropertyChanged(nameof(SelectedChartRow)); OnPropertyChanged(nameof(SelectedDetails)); OnPropertyChanged(nameof(SelectionPrompt));
    }
    public void SortGroups(BreakdownSortColumn column)
    {
        if (_presentation is null || !Enum.IsDefined(column)) return;
        _sortDescending = _sortColumn == column && !_sortDescending;
        _sortColumn = column;
        _groupRows = _presentation.Sort(column, _sortDescending);
        RebuildChart();
        NotifyTable();
    }
    private void NotifyTable()
    {
        ViewTradesCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(Chart)); NotifySelection();
        foreach (string name in new[] { nameof(GroupRows), nameof(SelectedGroup), nameof(SelectedGroupKey), nameof(SortColumn), nameof(SortDescending) })
            OnPropertyChanged(name);
    }
    public bool HasResult => Result is not null;
    public bool IsEmpty => Result?.Summary.EligibleTradeCount == 0;
    public bool HasUnappliedChanges => !TryQuery(out var query, out _, out _) || query != Result?.Query;
    public string DraftStatus => HasUnappliedChanges ? "Unapplied filters — choose Apply filters to use these values." : "Filters match the last successful result.";
    public string LookupWarning => !_account.Available || !_instrument.Available
        ? "A selected reference is unavailable. Its exact identity is retained; choose another explicitly or refresh this exact scope." : "";
    public string AppliedContext => Result is null ? "No successful result yet." :
        $"Last successful result · {Range(Result.Query)} · New York closure dates · {_resultAccount} · {_resultInstrument} · {Result.Query.Currency} · {Groupings.First(g => g.Value == Result.Query.Grouping).Name} · {Modes.First(m => m.Value == Result.Query.Mode).Name}";
    // Concise presentation of the successful scope, never of unapplied controls.
    public string CompactContext => Result is null ? "" :
        $"{Range(Result.Query)} · {_resultAccount} · {_resultInstrument} · {Result.Query.Currency} · New York closure dates";
    public string ChartHeading => Result is null ? "" :
        $"{(ChartMetric == BreakdownChartMetric.Pnl ? Presentation!.Summary.Basis + " P&L" : ChartMetric == BreakdownChartMetric.Trades ? "Closed Trades" : "Win Rate")} by {Groupings.First(g => g.Value == Result.Query.Grouping).Name.ToLowerInvariant()}";
    public string ResultStatus => Result is null ? "" : IsEmpty ? "No matching closed Trades." :
        $"{Result.Summary.EligibleTradeCount} closed Trades · {Result.Groups.Count} groups · {Result.Query.Currency} · {SourceName(Result.Summary.FinancialSources.Source)}";
    public string CoverageStatus => Result is null ? "" :
        $"Actual Net used: {Result.Summary.FinancialSources.NetUsedCount} · Gross fallback: {Result.Summary.FinancialSources.GrossFallbackCount} · Explicit Gross used: {Result.Summary.FinancialSources.GrossUsedCount} · Unavailable: {Result.Summary.FinancialSources.UnavailableCount}. Missing total costs: {Result.Summary.Data.MissingCostTradeCount}.";
    public Task LoadTask { get; private set; } = Task.CompletedTask;
    public IAsyncRelayCommand ApplyCommand { get; }
    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand ResetCommand { get; }
    public IRelayCommand CancelCommand { get; }
    private DateOnly Today => DateOnly.FromDateTime(TradingTimePolicy.ConvertUtcToTradingTime(_time.GetUtcNow()).DateTime);

    public Task ActivateAsync()
    {
        if (_disposed) return Task.CompletedTask;
        _active = true;
        if (_started) return IsLoading ? LoadTask : Task.CompletedTask;
        _started = true;
        if (!TryQuery(out var query, out _, out _)) return ApplyAsync();
        return LoadTask = LoadAsync(query!, initializeCurrency: _draftVersion == 0);
    }
    public void Deactivate()
    {
        _active = false;
        if (IsLoading) StatusMessage = "Loading stopped on leaving Breakdown. Apply or Refresh to load again.";
        InvalidateLoad();
    }
    public void Dispose() { if (_disposed) return; Deactivate(); _disposed = true; }
    public void OnDataCommitted()
    {
        if (_disposed) return;
        InvalidateLoad();
        IsStale = true;
        StatusMessage = "Trade data changed. Refresh to update the last submitted scope, or Apply filters.";
    }
    public Task ApplyAsync()
    {
        if (!_active || _disposed) return Task.CompletedTask;
        bool valid = TryQuery(out var query, out var dates, out var currency);
        DateValidation = dates; CurrencyValidation = currency;
        if (!valid)
        {
            InvalidateLoad(); ErrorMessage = "Correct the filters and apply again."; StatusMessage = "Filters were not applied.";
            FocusRequested?.Invoke(currency is not null ? "CurrencyFilter"
                : !Enum.IsDefined(Period) ? "PeriodFilter"
                : !Enum.IsDefined(Grouping) ? "GroupingFilter"
                : !Enum.IsDefined(Mode) ? "ModeFilter"
                : !ParseDate(StartDateText, out _) ? "FromDate" : "ThroughDate");
            return Task.CompletedTask;
        }
        return LoadTask = LoadAsync(query!);
    }
    public Task RefreshAsync() => !_active || _disposed ? Task.CompletedTask
        : _submitted is null ? ApplyAsync() : LoadTask = LoadAsync(_submitted);
    public Task ResetAsync()
    {
        if (!_active || _disposed) return Task.CompletedTask;
        _anchor = Today;
        Period = DashboardPeriod.All; SelectedAccount = Accounts[0]; SelectedInstrument = Instruments[0];
        Currency = _defaultCurrency; Grouping = BreakdownGrouping.Instrument; Mode = BreakdownCalculationMode.NetPreferred;
        StartDateText = EndDateText = "";
        return ApplyAsync();
    }
    private void Cancel()
    {
        InvalidateLoad(); ErrorMessage = null; StatusMessage = "Loading cancelled. Last successful result retained; Apply or Refresh to try again.";
        FocusRequested?.Invoke("RefreshButton");
    }
    public event Action<string>? FocusRequested;
    private void InvalidateLoad()
    {
        ++_generation;
        var previous = _cancellation; _cancellation = null;
        previous?.Cancel(); // The owning async operation disposes its own source in finally.
        IsLoading = false;
    }
    private async Task LoadAsync(BreakdownQuery query, bool initializeCurrency = false)
    {
        InvalidateLoad();
        long generation = _generation, draftVersion = _draftVersion;
        using var cancellation = new CancellationTokenSource();
        _cancellation = cancellation;
        _submitted = query;
        ErrorMessage = null; IsLoading = true; StatusMessage = "Loading Breakdown… Last successful result remains below.";
        try
        {
            // SQLite async calls and the complete calculator may execute synchronously.
            // Create/use/dispose all backend contexts inside the worker; publish on captured UI context.
            var loaded = await Task.Run(async () =>
            {
                var accounts = await _accounts.GetAllAsync(cancellation.Token);
                var instruments = await _instruments.GetAllAsync(cancellation.Token);
                var currencies = accounts.Select(a => a.Currency).Concat(instruments.Select(i => i.Currency))
                    .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
                var effective = initializeCurrency ? new BreakdownQuery(currencies.FirstOrDefault() ?? "USD",
                    query.TradeFilter.TradingAccountId, query.TradeFilter.InstrumentId,
                    query.TradeFilter.ClosedFromNewYork, query.TradeFilter.ClosedThroughNewYork, query.Mode, query.Grouping) : query;
                return (accounts, instruments, currencies, effective);
            }, cancellation.Token);
            if (!Owns(generation)) return;
            _submitted = loaded.effective;
            _defaultCurrency = loaded.currencies.FirstOrDefault() ?? "USD";
            if (initializeCurrency && _draftVersion == draftVersion) Currency = loaded.effective.Currency;
            Accounts = Options(loaded.accounts.Select(a => new BreakdownReferenceOption(a.Id, a.Name + (a.IsActive ? "" : " · inactive"))), _account, "All accounts");
            Instruments = Options(loaded.instruments.Select(i => new BreakdownReferenceOption(i.Id, i.Symbol + (i.IsActive ? "" : " · inactive"))), _instrument, "All instruments");
            SelectedAccount = Accounts.First(a => a.Id == _account.Id);
            SelectedInstrument = Instruments.First(i => i.Id == _instrument.Id);
            Currencies = loaded.currencies.Concat(new[] { Currency }).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            // Record the exact submitted scope before invoking the reader, including on cancellation.
            // Catalogs populate the controls only; result metadata still comes from the coherent reader snapshot.
            var result = await Task.Run(() => _reader.GetAsync(loaded.effective, cancellation.Token), cancellation.Token);
            if (!Owns(generation)) return;
            var presentation = new BreakdownPresentation(result);
            var rows = presentation.Sort(_sortColumn, _sortDescending);
            _resultAccount = loaded.accounts.FirstOrDefault(a => a.Id == loaded.effective.TradeFilter.TradingAccountId)?.Name
                ?? (loaded.effective.TradeFilter.TradingAccountId is null ? "All accounts" : "Unavailable Account (exact scope)");
            _resultInstrument = loaded.instruments.FirstOrDefault(i => i.Id == loaded.effective.TradeFilter.InstrumentId)?.Symbol
                ?? (loaded.effective.TradeFilter.InstrumentId is null ? "All instruments" : "Unavailable Instrument (exact scope)");
            _result = result;
            _presentation = presentation;
            _groupRows = rows;
            if (!rows.Any(r => r.Key == _selectedGroupKey)) _selectedGroupKey = null;
            RebuildChart();
            IsStale = false;
            StatusMessage = IsEmpty ? "Loaded an empty population." : "Breakdown loaded.";
            NotifyResult();
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        { if (Owns(generation)) StatusMessage = "Loading cancelled. Last successful result retained."; }
        catch (Exception)
        { if (Owns(generation)) { ErrorMessage = "Breakdown could not be loaded. Refresh to retry the submitted scope, or change filters and Apply."; StatusMessage = "Read failed; last successful result retained."; } }
        finally
        {
            if (Owns(generation)) { _cancellation = null; IsLoading = false; }
        }
    }
    private bool Owns(long generation) => !_disposed && _active && generation == _generation;
    private void Edited()
    {
        _draftVersion++;
        TryQuery(out _, out var dates, out var currency);
        DateValidation = dates; CurrencyValidation = currency;
        OnPropertyChanged(nameof(HasUnappliedChanges)); OnPropertyChanged(nameof(DraftStatus)); OnPropertyChanged(nameof(LookupWarning));
    }
    private void NotifyResult()
    {
        OnPropertyChanged(nameof(Presentation));
        NotifyTable();
        foreach (var property in new[] { nameof(Result), nameof(HasResult), nameof(IsEmpty), nameof(AppliedContext),
                     nameof(CompactContext), nameof(ChartHeading), nameof(ResultStatus), nameof(CoverageStatus), nameof(HasUnappliedChanges), nameof(DraftStatus), nameof(LookupWarning) })
            OnPropertyChanged(property);
    }
    private bool TryQuery(out BreakdownQuery? query, out string? dateError, out string? currencyError)
    {
        query = null; dateError = currencyError = null;
        if (!Enum.IsDefined(Period)) { dateError = "Select a supported period."; return false; }
        DateOnly? start = null, end = null;
        if (Period == DashboardPeriod.Custom)
        {
            if (!ParseDate(StartDateText, out start) || !ParseDate(EndDateText, out end))
                dateError = "Enter dates as yyyy-MM-dd, or leave a bound empty for all history on that side.";
        }
        else if (Period != DashboardPeriod.All)
        {
            start = Period == DashboardPeriod.Week ? DashboardMetricCalculator.GetWeekStartingMonday(_anchor)
                : Period == DashboardPeriod.Month ? new(_anchor.Year, _anchor.Month, 1) : new DateOnly(_anchor.Year, 1, 1);
            end = Period == DashboardPeriod.Week ? start.Value.AddDays(6)
                : Period == DashboardPeriod.Month ? start.Value.AddMonths(1).AddDays(-1) : new(_anchor.Year, 12, 31);
        }
        if (start > end || end == DateOnly.MaxValue) dateError = "The end must be on or after the start and before 9999-12-31.";
        if (string.IsNullOrWhiteSpace(Currency) || Currency.Trim().Length > 8) currencyError = "Enter a historical pricing currency (1–8 characters).";
        if (dateError is not null || currencyError is not null) return false;
        try { query = new(Currency, _account.Id, _instrument.Id, start, end, Mode, Grouping); return true; }
        catch (ArgumentException) { dateError = "Select a supported period, grouping and calculation mode with valid dates."; return false; }
    }
    private static bool ParseDate(string text, out DateOnly? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(text)) return true;
        if (!DateOnly.TryParseExact(text.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) return false;
        value = date; return true;
    }
    private static IReadOnlyList<BreakdownReferenceOption> Options(IEnumerable<BreakdownReferenceOption> values, BreakdownReferenceOption selected, string all)
    {
        var options = values.OrderBy(v => v.Name, StringComparer.CurrentCulture).ThenBy(v => v.Id).ToList();
        options.Insert(0, new(null, all));
        if (!options.Any(v => v.Id == selected.Id)) options.Add(selected.Available
            ? new(selected.Id, $"{selected.Name} · unavailable", false) : selected);
        return options;
    }
    private static string Range(BreakdownQuery query) => query.TradeFilter.ClosedFromNewYork is null && query.TradeFilter.ClosedThroughNewYork is null
        ? "All history" : $"{query.TradeFilter.ClosedFromNewYork?.ToString("dd MMM yyyy") ?? "All earlier dates"} – {query.TradeFilter.ClosedThroughNewYork?.ToString("dd MMM yyyy") ?? "All later dates"}";
    private static string SourceName(BreakdownFinancialSource source) => source switch
    { BreakdownFinancialSource.NetGross => "Net/Gross", BreakdownFinancialSource.GrossFallback => "Gross fallback", BreakdownFinancialSource.Gross => "Explicit Gross", BreakdownFinancialSource.Net => "Net", _ => "No evaluable financial values" };
}
