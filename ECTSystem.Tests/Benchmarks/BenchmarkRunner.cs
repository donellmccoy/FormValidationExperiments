using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

namespace ECTSystem.Tests.Benchmarks;

/// <summary>
/// Entry point for running BenchmarkDotNet benchmarks.
/// <para>
/// Usage (from solution root):
///   dotnet run -c Release --project ECTSystem.Tests -- --filter *MapperBenchmarks*
///   dotnet run -c Release --project ECTSystem.Tests -- --filter *StateMachineBenchmarks*
///   dotnet run -c Release --project ECTSystem.Tests -- --filter *Benchmarks*
/// </para>
/// <para>
/// Alternatively, run all benchmarks from xUnit (Debug/CI mode with reduced iteration count):
///   dotnet test --filter "FullyQualifiedName~Benchmarks.BenchmarkSmokeTests"
/// </para>
/// </summary>
public static class BenchmarkEntryPoint
{
    /// <summary>
    /// Run this from the command line for full benchmark reports.
    /// Since this is a test project (not console app), use the xUnit smoke tests below for CI.
    /// </summary>
    public static void RunAll()
    {
        var config = DefaultConfig.Instance
            .WithOptions(ConfigOptions.DisableOptimizationsValidator);

        BenchmarkRunner.Run<MapperBenchmarks>(config);
        BenchmarkRunner.Run<StateMachineBenchmarks>(config);
    }
}
