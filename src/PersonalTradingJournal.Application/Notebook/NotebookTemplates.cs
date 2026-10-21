using System.Collections.Immutable;
using PersonalTradingJournal.Domain.Notebook;

namespace PersonalTradingJournal.Application.Notebook;

public enum NotebookTemplate { BlankNote, MarketStudy, TradingIdea, LessonLearned, Psychology }
public sealed record NotebookTemplateDefinition(NotebookTemplate Id, string Name, ImmutableArray<string> Sections);

/// <summary>Built-in instructions, never a persisted catalog or shared user document.</summary>
public static class NotebookTemplates
{
    public static ImmutableArray<NotebookTemplateDefinition> All { get; } =
    [
        new(NotebookTemplate.BlankNote, "Blank Note", []),
        new(NotebookTemplate.MarketStudy, "Market Study", ["Context", "Observation", "Examples", "Conclusion", "Open questions"]),
        new(NotebookTemplate.TradingIdea, "Trading Idea", ["Idea", "Conditions", "Invalidation", "Evidence to collect"]),
        new(NotebookTemplate.LessonLearned, "Lesson Learned", ["Situation", "Observation", "Lesson", "Next action"]),
        new(NotebookTemplate.Psychology, "Psychology", ["Trigger", "Thoughts", "Behavior", "Alternative response"])
    ];

    public static NotebookDocument Create(NotebookTemplate template)
    {
        var definition = All.SingleOrDefault(x => x.Id == template) ?? throw new ArgumentOutOfRangeException(nameof(template));
        // The agreed templates contain headings only. Every invocation owns fresh nodes.
        return new(definition.Sections.Select(section => NotebookNode.Heading(Guid.NewGuid(), 2,
            [NotebookNode.Run(Guid.NewGuid(), section)])));
    }
}
