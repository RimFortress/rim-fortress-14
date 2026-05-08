using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Content.IntegrationTests;
using Content.IntegrationTests.Pair;
using Content.Server._RF.NPC.GOAP.Systems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Prototypes;
using Content.Shared._RF.NPC.UtilityAi.Prototypes;
using Robust.Shared;
using Robust.Shared.Analyzers;
using Robust.Shared.CPUJob.JobQueues;
using Robust.Shared.CPUJob.JobQueues.Queues;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Benchmarks._RF;

[Virtual]
[SimpleJob(
    runStrategy: RunStrategy.Throughput,
    launchCount: 1,
    warmupCount: 1,
    iterationCount: 3)]
[InvocationCount(1)]
[MemoryDiagnoser]
public class GoapPlanningBenchmark
{
    public static readonly ProtoId<GoapCompoundPrototype> RootTask = "Idle";

    private TestPair _pair = default!;
    private GoapSystem _goap = default!;
    private IPrototypeManager _proto = default!;

    private Entity<GoapComponent> _target;
    private GoapState _startState = default!;

    private IReadOnlyList<ExecutableGoapTask> _tasks = default!;
    private JobQueue _planQueue = new(0.04f);
    private List<GoapPlanJob> _jobs = new();

    public static string[] GoalSource { get; } =
    {
        "Sleep",
        "WakeUp",
        "Idle",
        "MoveTo",
        "Eat",
        "Drink",
    };

    [ParamsSource(nameof(GoalSource))]
    public string Goal;

    [GlobalSetup]
    public async Task Setup()
    {
        _pair = await GoapBenchmarkHost.GetOrCreatePairAsync();

        var server = _pair.Server;
        var mapData = await _pair.CreateTestMap();

        await server.WaitPost(() =>
        {
            _goap = server.EntMan.System<GoapSystem>();
            _proto = server.ResolveDependency<IPrototypeManager>();

            var uid = server.EntMan.SpawnEntity(null, mapData.GridCoords);
            var comp = server.EntMan.ComponentFactory.GetComponent<GoapComponent>();
            comp.RootTask = RootTask;
            comp.Enabled = false;
            comp.State.SetValue(GoapState.Owner, uid);

            server.EntMan.AddComponent(uid, comp);

            _startState = comp.State;
            _target = new(uid, comp);
            _tasks = _goap.GetExecutableTasks(RootTask);
        });
    }

    [IterationSetup]
    public void IterationSetup()
    {
        _jobs.Clear();
        _planQueue = new JobQueue(0.04f);
    }

    [Benchmark]
    public async Task QueueAndRunAllGoals()
    {
        await _pair.Server.WaitPost(() =>
        {
            var proto = _proto.Index<UtilityAiGoalPrototype>(Goal);
            var planJob = new GoapPlanJob(
                maxTime: 0.04,
                goap: _goap,
                target: _target.Owner,
                startState: _startState.ShallowClone(),
                goalState: proto.GoalState.ShallowClone(),
                tasks: _tasks);

            _jobs.Add(planJob);
            _planQueue.EnqueueJob(planJob);
        });

        while (_jobs.Exists(x => x.Status != JobStatus.Finished))
        {
            _planQueue.Process();
            await _pair.RunTicksSync(1);
        }

        foreach (var job in _jobs)
        {
            if (job.Exception != null)
                throw job.Exception;
        }
    }
}

internal static class GoapBenchmarkHost
{
    private static readonly object Sync = new();
    private static bool _initialized;
    private static TestPair _pair;

    public static async Task<TestPair> GetOrCreatePairAsync()
    {
        if (_initialized && _pair is not null)
            return _pair;

        lock (Sync)
        {
            if (_initialized && _pair is not null)
                return _pair;
        }

#if !DEBUG
        ProgramShared.PathOffset = "../../../../";
#endif

        // PoolManager must be started only once per process.
        PoolManager.Startup();

        var pair = await PoolManager.GetServerClient(
            testContext: new ExternalTestContext("Benchmark", StreamWriter.Null));

        lock (Sync)
        {
            if (!_initialized)
            {
                _pair = pair;
                _initialized = true;
            }
        }

        return _pair!;
    }
}
