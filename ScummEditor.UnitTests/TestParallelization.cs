using Xunit;

// The real-data tests load whole SCUMM games (the v5/v6/v7 data files are tens to hundreds of MB),
// decode hundreds of bitmaps and copy game files to temp folders. Running those test classes in
// parallel contends for GDI handles, memory and the on-disk game files, which made the heavy
// graphics export/import tests flaky (non-deterministic IO locks / failures). Run the suite serially
// for deterministic results - the integration tests dominate the runtime either way.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
