using System;
using System.IO;
using System.Threading.Tasks;
using Content.IntegrationTests;
using Content.IntegrationTests.Pair;
using Robust.Shared;

namespace Content.Benchmarks._RF.Suil;

/// <summary>
/// One client/server pair for the SUIL benchmarks, because <see cref="PoolManager"/> may be started once per process.
/// </summary>
internal static class SuilBenchmarkHost
{
    private static Task<TestPair> _pair;

    public static Task<TestPair> GetPair() => _pair ??= Create();

    private static async Task<TestPair> Create()
    {
        // The engine looks for "../../RobustToolbox" and "../../Resources" behind this offset, as if run from
        // bin/<project>. The default offset of the other benchmarks is for the folder BenchmarkDotNet builds into;
        // this works wherever (and however) the benchmark is run.
        var output = Path.Combine(RepositoryRoot(), "bin", "Content.Benchmarks");
        ProgramShared.PathOffset = Path.GetRelativePath(Directory.GetCurrentDirectory(), output) + Path.DirectorySeparatorChar;

        try
        {
            PoolManager.Startup();
        }
        catch (InvalidOperationException)
        {
            // Started by another benchmark of this process.
        }

        return await PoolManager.GetServerClient(
            testContext: new ExternalTestContext("Benchmark", StreamWriter.Null));
    }

    /// <summary>
    /// The folder with <c>Resources</c> and <c>RobustToolbox</c>.
    /// </summary>
    private static string RepositoryRoot()
    {
        for (var dir = AppContext.BaseDirectory; dir != null; dir = Path.GetDirectoryName(dir))
        {
            if (Directory.Exists(Path.Combine(dir, "RobustToolbox")) && Directory.Exists(Path.Combine(dir, "Resources")))
                return dir;
        }

        throw new DirectoryNotFoundException("Repository root not found above " + AppContext.BaseDirectory);
    }
}
