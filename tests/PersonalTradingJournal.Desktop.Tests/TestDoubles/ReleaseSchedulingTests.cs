using System.Reflection;
using PersonalTradingJournal.Desktop.Tests.CalendarPage;
using PersonalTradingJournal.Desktop.Tests.Journals;
using PersonalTradingJournal.Desktop.Tests.Notebook;
using PersonalTradingJournal.Desktop.Tests.TradingPlans;

namespace PersonalTradingJournal.Desktop.Tests.TestDoubles;

public sealed class ReleaseSchedulingTests
{
    [Fact]
    public void ExclusiveTraitAndCollectionCoverRequiredNativeClassesWithoutSerializingOtherTests()
    {
        Type[] expected = [typeof(CalendarDayModalTests), typeof(CalendarDayPerformanceChartTests), typeof(JournalViewTests), typeof(NotebookLifecycleViewTests), typeof(NotebookFolderCreationViewTests), typeof(NotebookFolderActionsViewTests), typeof(NotebookHeaderViewTests),
            typeof(TradingPlanWorkspaceViewTests), typeof(SessionPreparationViewTests), typeof(PlanSectionsViewTests)];
        Type[] exclusive = typeof(ReleaseSchedulingTests).Assembly.GetTypes()
            .Where(type => type.CustomAttributes.Any(trait => trait.AttributeType == typeof(TraitAttribute) &&
                Equals(trait.ConstructorArguments[0].Value, ExclusiveWpfCollection.Trait) &&
                Equals(trait.ConstructorArguments[1].Value, ExclusiveWpfCollection.Value)))
            .ToArray();
        Assert.Equal(expected.OrderBy(t => t.FullName), exclusive.OrderBy(t => t.FullName));
        foreach (Type type in exclusive)
            Assert.Equal(ExclusiveWpfCollection.Name,
                Assert.Single(type.CustomAttributes, a => a.AttributeType == typeof(CollectionAttribute)).ConstructorArguments[0].Value);
        Assert.True(typeof(ExclusiveWpfCollection).GetCustomAttribute<CollectionDefinitionAttribute>()!.DisableParallelization);
        var assembly = typeof(ReleaseSchedulingTests).Assembly.GetCustomAttribute<CollectionBehaviorAttribute>()!;
        Assert.False(assembly.DisableTestParallelization);
        Assert.Equal(2, assembly.MaxParallelThreads);
        // These tests deliberately create concurrent work themselves; neither
        // their bodies nor their placement is changed by the resource lane.
        Assert.Null(typeof(CalendarStaTestTests).GetCustomAttribute<CollectionAttribute>());
    }
}
