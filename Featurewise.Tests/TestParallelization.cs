// Tests that listen for activities hear every invocation in the process, so test classes run one
// at a time rather than in parallel.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
