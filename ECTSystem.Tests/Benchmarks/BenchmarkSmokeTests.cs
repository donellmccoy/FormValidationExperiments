using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using Xunit;

namespace ECTSystem.Tests.Benchmarks;

/// <summary>
/// xUnit-integrated smoke tests that run BenchmarkDotNet in a short "dry run" mode.
/// These validate that benchmarks compile and execute correctly without running
/// full statistical analysis (which takes minutes).
/// </summary>
[Trait("Category", "Benchmark")]
public class BenchmarkSmokeTests
{
    private static readonly IConfig ShortRunConfig = DefaultConfig.Instance
        .WithOptions(ConfigOptions.DisableOptimizationsValidator)
        .AddJob(Job.Dry);

    [Fact]
    public void MapperBenchmarks_DryRun()
    {
        var summary = BenchmarkRunner.Run<MapperBenchmarks>(ShortRunConfig);
        Assert.False(summary.HasCriticalValidationErrors, "MapperBenchmarks has validation errors");
    }

    [Fact]
    public void StateMachineBenchmarks_DryRun()
    {
        var summary = BenchmarkRunner.Run<StateMachineBenchmarks>(ShortRunConfig);
        Assert.False(summary.HasCriticalValidationErrors, "StateMachineBenchmarks has validation errors");
    }
}
