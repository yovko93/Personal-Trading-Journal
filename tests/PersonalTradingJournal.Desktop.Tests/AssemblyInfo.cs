// Desktop collections can each start a native renderer or a supervised child.
// Bound that fan-out rather than multiplying it by the runner's CPU count while
// the other three assemblies also run. Two collections remain concurrent; this
// does not serialize the solution, change process deadlines or cap ThreadPool
// workers used by the application's explicitly concurrent regression scenarios.
[assembly: Xunit.CollectionBehavior(MaxParallelThreads = 2)]
