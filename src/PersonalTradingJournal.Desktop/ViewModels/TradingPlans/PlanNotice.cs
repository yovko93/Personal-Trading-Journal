namespace PersonalTradingJournal.Desktop.ViewModels.TradingPlans;

public enum PlanNoticeKind { Info, Success, Warning, Error }

/// <summary>Presentation only: operation outcomes remain owned by the existing commands.</summary>
public sealed record PlanNotice(string Text, PlanNoticeKind Kind = PlanNoticeKind.Info)
{
    public static PlanNoticeKind ForWrite(PersonalTradingJournal.Application.TradingPlans.TradingPlanWriteStatus status) => status switch
    {
        PersonalTradingJournal.Application.TradingPlans.TradingPlanWriteStatus.Success => PlanNoticeKind.Success,
        PersonalTradingJournal.Application.TradingPlans.TradingPlanWriteStatus.Cancelled => PlanNoticeKind.Info,
        PersonalTradingJournal.Application.TradingPlans.TradingPlanWriteStatus.Invalid or
        PersonalTradingJournal.Application.TradingPlans.TradingPlanWriteStatus.ReferenceChanged => PlanNoticeKind.Warning,
        _ => PlanNoticeKind.Error,
    };
    public bool HasText => !string.IsNullOrWhiteSpace(Text);
    public string Icon => Kind switch { PlanNoticeKind.Success => "✓", PlanNoticeKind.Warning => "⚠", PlanNoticeKind.Error => "!", _ => "ⓘ" };
    public string AccessibleText => $"{Kind}: {Text}";
}
