namespace PersonalTradingJournal.Desktop.Tests.TestDoubles;

// The Release gate runs this trait before the parallel remainder. The collection
// also prevents foreground-dependent families from overlapping each other (or other
// Desktop collections in an IDE run). Explicit concurrency inside tests remains.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ExclusiveWpfCollection
{
    public const string Name = "Resource-intensive WPF children";
    public const string Trait = "Workload";
    public const string Value = "ExclusiveWpf";
}
