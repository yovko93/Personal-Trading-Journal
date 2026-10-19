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
                errors.Add(new("Scenarios", "Stand-aside-only mode excludes trading scenarios. Turn off Stand aside only to keep scenarios and ordinary no-trade conditions together."));
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
            if (scenario.Direction is null) errors.Add(new($"Scenarios[{i}].Direction", "Select Long or Short."));
        }

        return errors.ToImmutable();
    }

}
