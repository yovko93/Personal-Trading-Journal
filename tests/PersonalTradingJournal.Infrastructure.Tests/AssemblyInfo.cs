// Migration, archive and SQLite fixtures compete with the parallel Desktop
// assembly's supervised WPF processes for CPU and storage. Bound collection
// fan-out independently of the host CPU count instead of extending UI deadlines.
// Four collections remain concurrent; explicit concurrent-writer/reader tests
// retain their own concurrency, and the solution still runs assemblies in parallel.
[assembly: Xunit.CollectionBehavior(MaxParallelThreads = 4)]
