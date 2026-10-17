using System.Collections.Immutable;

namespace PersonalTradingJournal.Domain.TradingPlans;

public sealed record TradingPlanValidationIssue(string Field, string Message);

/// <summary>Content-only Ready validation. Storage must also recheck exact live references
/// and concurrency inside the committing transaction. An empty result is not a persisted Ready plan.</summary>
public static class TradingPlanReadiness
{
    public static ImmutableArray<TradingPlanValidationIssue> Validate(TradingPlanContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var errors = ImmutableArray.CreateBuilder<TradingPlanValidationIssue>();
        void Required(string value, string field, string message)
        {
            if (string.IsNullOrWhiteSpace(value)) errors.Add(new(field, message));
        }

        if (content.Session is null) errors.Add(new("Session", "Select the intended session."));
        Required(content.Focus, "Focus", "Enter today's behavioral focus.");
        if (content.Instruments.IsEmpty) errors.Add(new("Instruments", "Select at least one Instrument context."));
        if (content.Intent == TradingPlanIntent.StandAside)
        {
            Required(content.StandAside, "StandAside", "Explain the decision to stand aside.");
            if (!content.Scenarios.IsEmpty)
                errors.Add(new("Scenarios", "Remove trading scenarios or choose an opportunity plan."));
        }
        else
        {
            if (content.Scenarios.IsEmpty) errors.Add(new("Scenarios", "Define a scenario or explicitly choose to stand aside."));
            for (int i = 0; i < content.Instruments.Length; i++)
            {
                var instrument = content.Instruments[i];
                if (instrument.Bias is null) errors.Add(new($"Instruments[{i}].Bias", "Select a bias."));
                Required(instrument.MarketThesis, $"Instruments[{i}].MarketThesis", "Describe the market context.");
            }
        }

        for (int i = 0; i < content.Scenarios.Length; i++)
        {
            var scenario = content.Scenarios[i];
            Required(scenario.Name, $"Scenarios[{i}].Name", "Name the scenario.");
            if (scenario.Direction is null) errors.Add(new($"Scenarios[{i}].Direction", "Select Long or Short."));
            Required(scenario.Condition, $"Scenarios[{i}].Condition", "Describe the observable IF condition.");
            Required(scenario.Response, $"Scenarios[{i}].Response", "Describe the intended THEN response.");
            Required(scenario.Invalidation, $"Scenarios[{i}].Invalidation", "Describe what invalidates the scenario.");
        }

        foreach (var check in new[] { TradingPlanChecklist.ContextAndLevels, TradingPlanChecklist.ScenariosOrStandAside,
            TradingPlanChecklist.RiskLimits, TradingPlanChecklist.News, TradingPlanChecklist.MindsetAndFocus })
            if (!content.Confirmed.HasFlag(check)) errors.Add(new($"Checklist.{check}", ChecklistMessage(check)));
        return errors.ToImmutable();
    }

    private static string ChecklistMessage(TradingPlanChecklist check) => check switch
    {
        TradingPlanChecklist.ContextAndLevels => "Confirm that context and available levels were reviewed.",
        TradingPlanChecklist.ScenariosOrStandAside => "Confirm the scenarios or stand-aside decision.",
        TradingPlanChecklist.RiskLimits => "Confirm that planned limits, including any unset limits, were reviewed.",
        TradingPlanChecklist.News => "Confirm that the news calendar was reviewed manually.",
        _ => "Confirm that mindset and focus were reviewed."
    };
}
