#nullable enable
using System.Linq;
using System.Numerics;
using Content.Client._RF.NPC.Pathfinding.Debug;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server._RF.NPC.Pathfinding.Systems;
using Content.Server._RF.NPC.Steering.Components;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.Pathfinding;
using Content.Shared._RF.NPC.Pathfinding.Algorithms;
using Content.Shared._RF.NPC.Pathfinding.Components;
using Robust.Shared.CPUJob.JobQueues;
using Robust.Shared.CPUJob.JobQueues.Queues;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._RF.NPC.Pathfinding;

/// <summary>
/// What the debug overlays of the pathfinding are told by the server: the navigation map, the costs of an agent by a profile and a
/// query, the influence of a tag, and the routes with what their searches looked at.
/// </summary>
[TestOf(typeof(RfPathfindingDebugSystem))]
public sealed class PathDebugTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: pathTag
  id: TestDebugLava
  influence: 2

- type: entity
  id: TestDebugWall
  components:
  - type: Physics
    bodyType: Static
  - type: Fixtures
    fixtures:
      fix1:
        shape:
          !type:PhysShapeAabb
          bounds: ""-0.5,-0.5,0.5,0.5""
        mask:
        - MobMask
        layer:
        - Impassable
  - type: Damageable
    damageContainer: StructuralInorganic
  - type: Destructible
    thresholds:
    - trigger:
        !type:DamageTypeTrigger
        damageType: Structural
        damage: 100
      behaviors:
      - !type:DoActsBehavior
        acts: [ ""Destruction"" ]

- type: entity
  id: TestDebugLavaSource
  components:
  - type: PathTags
    tags:
    - TestDebugLava

- type: goapCompound
  id: TestDebugRoot
  tasks:
  - preconditions: []
    action: !type:MoveTo
    effects:
      Noop: true

- type: pathProfile
  id: TestDebugWalker
  rules:
  - when:
    - !type:Blocked
    cost:
    - float: 10000

- type: pathProfile
  id: TestDebugLenient
  rules:
  - when:
    - !type:Blocked
    cost:
    - float: 20

# The rules of a profile can be for a query only.
- type: pathQuery
  id: TestDebugStrict
  algorithm: !type:AStar
    maxCost: 5000
    allowPartial: false

- type: pathQuery
  id: TestDebugLoose

- type: pathProfile
  id: TestDebugByQuery
  rules:
  - when:
    - !type:Blocked
    - !type:Query
      id: TestDebugStrict
    cost:
    - float: 10000
  - when:
    - !type:Blocked
    - !type:Query
      id: TestDebugStrict
      invert: true
    cost:
    - float: 20

- type: entity
  id: TestDebugAgent
  components:
  - type: Goap
    rootTask: TestDebugRoot
  - type: PathProfiles
    profiles:
    - TestDebugWalker
  - type: Physics
    bodyType: Static
  - type: Fixtures
    fixtures:
      fix1:
        shape:
          !type:PhysShapeAabb
          bounds: ""-0.25,-0.25,0.25,0.25""
        mask:
        - MobMask
        layer:
        - MobLayer
";

    [SidedDependency(Side.Client)] private readonly PathfindingDebugSystem _clientDebug = default!;

    private static readonly Box2 Everything = new(-100, -100, 100, 100);

    [SidedDependency(Side.Server)] private readonly RfPathfindingSystem _pathfinding = default!;
    [SidedDependency(Side.Server)] private readonly RfPathfindingDebugSystem _debug = default!;
    [SidedDependency(Side.Server)] private readonly SharedMapSystem _maps = default!;
    [SidedDependency(Side.Server)] private readonly IEntityManager _entMan = default!;

    private Entity<MapGridComponent> _grid;
    private EntityUid _agent;

    private async Task CreateWorld(params (int X, int Y, string Proto)[] things)
    {
        var map = await Pair.CreateTestMap();
        _grid = map.Grid;

        await Pair.Server.WaitPost(() =>
        {
            for (var x = 0; x < 8; x++)
            {
                for (var y = 0; y < 8; y++)
                {
                    _maps.SetTile(_grid.Owner, _grid.Comp, new Vector2i(x, y), new Tile(1));
                }
            }

            foreach (var (x, y, proto) in things)
            {
                _entMan.SpawnAtPosition(proto, new EntityCoordinates(_grid, x + 0.5f, y + 0.5f));
            }

            _agent = _entMan.SpawnAtPosition("TestDebugAgent", new EntityCoordinates(_grid, 0.5f, 7.5f));
        });

        await Pair.RunTicksSync(5);

        // The chunks are built when something needs them.
        await Pair.Server.WaitPost(() => _pathfinding.EnsureChunk(_grid, Vector2i.Zero));
    }

    private PathDebugResponse Ask(
        PathDebugLayers layers,
        string? profile = null,
        string? query = null,
        string? tag = null,
        EntityUid? agent = null)
        => _debug.Build(new PathDebugRequest(
            layers,
            Everything,
            profile,
            query,
            tag,
            agent == null ? null : _entMan.GetNetEntity(agent.Value)));

    private static PathDebugPoly? PolyAt(PathDebugGrid? grid, Vector2 point)
        => grid?.Polys.Cast<PathDebugPoly?>().FirstOrDefault(x => x!.Value.Box.Contains(point));

    [Test]
    public async Task TestMeshTellsWhatIsInThePolys()
    {
        await CreateWorld((3, 3, "TestDebugWall"));

        await Pair.Server.WaitAssertion(() =>
        {
            var response = Ask(PathDebugLayers.Mesh);
            var grid = response.Mesh.SingleOrDefault();
            Assert.That(grid, Is.Not.Null);
            Assert.That(grid!.Grid, Is.EqualTo(_entMan.GetNetEntity(_grid.Owner)));

            var wall = PolyAt(grid, new Vector2(3.5f, 3.5f));
            Assert.That(wall, Is.Not.Null);
            var kind = (PathDebugPolyKind) wall!.Value.Kind;
            Assert.That(kind, Is.EqualTo(PathDebugPolyKind.Solid | PathDebugPolyKind.Destroyable).Or.Not.EqualTo(PathDebugPolyKind.None));
            Assert.That(kind.HasFlag(PathDebugPolyKind.Solid), Is.True, "a body is there");
            Assert.That(kind.HasFlag(PathDebugPolyKind.Destroyable), Is.True, "and it can be broken");

            var floor = PolyAt(grid, new Vector2(6.5f, 1.5f));
            Assert.That(floor, Is.Not.Null);
            Assert.That(floor!.Value.Kind, Is.EqualTo((byte) PathDebugPolyKind.None), "nothing is on the floor");
            Assert.That(response.Cost, Is.Empty, "only what is asked for");
            Assert.That(response.Info, Does.Contain("mesh:"));
        });
    }

    [Test]
    public async Task TestCostIsWhatTheRulesOfTheProfileSay()
    {
        await CreateWorld((3, 3, "TestDebugWall"));

        await Pair.Server.WaitAssertion(() =>
        {
            var walker = Ask(PathDebugLayers.Cost, "TestDebugWalker", agent: _agent);
            var wall = PolyAt(walker.Cost.Single(), new Vector2(3.5f, 3.5f))!.Value;
            Assert.That(wall.Value, Is.GreaterThanOrEqualTo(RfPathfindingDebugSystem.Impassable), "a wall is not gone through");
            Assert.That(((PathDebugPolyKind) wall.Kind).HasFlag(PathDebugPolyKind.Blocked), Is.True);
            Assert.That(PolyAt(walker.Cost.Single(), new Vector2(6.5f, 1.5f))!.Value.Value, Is.EqualTo(1f).Within(0.01f), "a node costs 1 by itself");

            // Another profile, another price.
            var lenient = Ask(PathDebugLayers.Cost, "TestDebugLenient", agent: _agent);
            Assert.That(PolyAt(lenient.Cost.Single(), new Vector2(3.5f, 3.5f))!.Value.Value, Is.EqualTo(21f).Within(0.01f));

            Assert.That(walker.Info, Does.Contain("TestDebugWalker"));
            Assert.That(lenient.Info, Does.Contain("TestDebugLenient"));
        });
    }

    [Test]
    public async Task TestCostDependsOnTheQuery()
    {
        await CreateWorld((3, 3, "TestDebugWall"));

        await Pair.Server.WaitAssertion(() =>
        {
            var strict = Ask(PathDebugLayers.Cost, "TestDebugByQuery", "TestDebugStrict", agent: _agent);
            var loose = Ask(PathDebugLayers.Cost, "TestDebugByQuery", "TestDebugLoose", agent: _agent);

            Assert.That(PolyAt(strict.Cost.Single(), new Vector2(3.5f, 3.5f))!.Value.Value, Is.GreaterThanOrEqualTo(10000f));
            Assert.That(PolyAt(loose.Cost.Single(), new Vector2(3.5f, 3.5f))!.Value.Value, Is.EqualTo(21f).Within(0.01f));
        });
    }

    [Test]
    public async Task TestCostDoesNotTouchTheRequestOfTheAgent()
    {
        await CreateWorld((3, 3, "TestDebugWall"));

        await Pair.Server.WaitAssertion(() =>
        {
            var comp = _entMan.GetComponent<PathProfilesComponent>(_agent);
            var profile = comp.ActiveProfile;
            var rules = comp.ActiveRules.Count;

            Ask(PathDebugLayers.Cost, "TestDebugLenient", agent: _agent);

            Assert.That(comp.ActiveProfile, Is.EqualTo(profile), "the agent keeps its own request");
            Assert.That(comp.ActiveRules, Has.Count.EqualTo(rules));
        });
    }

    [Test]
    public async Task TestCostSaysWhenThereIsNoProfileOrAgent()
    {
        await CreateWorld();

        await Pair.Server.WaitAssertion(() =>
        {
            var response = Ask(PathDebugLayers.Cost, "NoSuchProfile", agent: _agent);
            Assert.That(response.Cost, Is.Empty);
            Assert.That(response.Info, Does.Contain("NoSuchProfile"));

            // No agent is named: the closest to the middle of the area is the one.
            var any = Ask(PathDebugLayers.Cost, "TestDebugWalker");
            Assert.That(any.Cost, Is.Not.Empty);
            Assert.That(any.Info, Does.Contain("agent:"));
        });
    }

    [Test]
    public async Task TestInfluenceIsOfTheChosenTag()
    {
        await CreateWorld((4, 4, "TestDebugLavaSource"));

        await Pair.Server.WaitAssertion(() =>
        {
            var lava = Ask(PathDebugLayers.Influence, tag: "TestDebugLava");
            var grid = lava.Influence.Single();
            var near = PolyAt(grid, new Vector2(4.5f, 4.5f))!.Value.Value;

            Assert.That(near, Is.GreaterThan(0f));
            Assert.That(PolyAt(grid, new Vector2(0.5f, 0.5f)), Is.Null, "no influence out of the radius");

            var none = Ask(PathDebugLayers.Influence, tag: "NoSuchTag");
            Assert.That(none.Influence, Is.Empty);
            Assert.That(none.Info, Does.Contain("NoSuchTag"));
        });
    }

    [Test]
    public async Task TestOnlyTheAreaIsSent()
    {
        await CreateWorld((3, 3, "TestDebugWall"));

        await Pair.Server.WaitAssertion(() =>
        {
            var all = Ask(PathDebugLayers.Mesh).Mesh.Single().Polys.Count;
            var request = new PathDebugRequest(PathDebugLayers.Mesh, new Box2(100, 100, 120, 120), null, null, null, null);
            Assert.That(_debug.Build(request).Mesh, Is.Empty, "nothing is far from the grid");
            Assert.That(all, Is.GreaterThan(0));
        });
    }

    [Test]
    public async Task TestRoutesShowWhatTheLastSearchLookedAt()
    {
        await CreateWorld((3, 3, "TestDebugWall"));

        await Pair.Server.WaitPost(() =>
        {
            _entMan.AddComponent<RfSteererComponent>(_agent);
            _pathfinding.DebugCaptureUntil = TimeSpan.FromHours(1);
        });

        await Pair.Server.WaitAssertion(() =>
        {
            var agent = new Entity<PathProfilesComponent, GoapComponent>(
                _agent,
                _entMan.GetComponent<PathProfilesComponent>(_agent),
                _entMan.GetComponent<GoapComponent>(_agent));
            Assert.That(_pathfinding.BeginRequest(agent, "TestDebugWalker", "TestDebugLoose"), Is.True);

            var job = new PathJob(
                0.05,
                agent,
                new EntityCoordinates(_grid, 0.5f, 3.5f),
                new EntityCoordinates(_grid, 6.5f, 3.5f),
                0.4f,
                new AStar(),
                _pathfinding);
            var queue = new JobQueue();
            queue.EnqueueJob(job);

            for (var i = 0; i < 100000 && job.Status != Robust.Shared.CPUJob.JobQueues.JobStatus.Finished; i++)
            {
                queue.Process();
            }

            Assert.That(job.Status, Is.EqualTo(Robust.Shared.CPUJob.JobQueues.JobStatus.Finished));

            var response = Ask(PathDebugLayers.Routes);
            var route = response.Routes.SingleOrDefault();
            Assert.That(route, Is.Not.Null, "an agent that has searched is shown");
            Assert.That(route!.Agent, Is.EqualTo(_entMan.GetNetEntity(_agent)));
            Assert.That(route.Explored, Is.Not.Empty, "the nodes that the search has looked at");
            Assert.That(route.Explored.Any(x => x.Closed), Is.True, "and the ones it expanded");
            Assert.That(route.Explored.All(x => x.G >= 0f), Is.True);
            Assert.That(route.ExploredGraph, Is.EqualTo(_entMan.GetNetEntity(_grid.Owner)));

            // The routes can be told by the query and by the profile.
            Assert.That(Ask(PathDebugLayers.Routes, query: "TestDebugStrict").Routes, Is.Empty);

            // The system is the same for the next tests of the pool.
            _pathfinding.DebugCaptureUntil = TimeSpan.Zero;
            _pathfinding.Explored.Clear();
        });
    }

    [Test]
    public async Task TestSearchesAreNotKeptWhenNobodyLooks()
    {
        await CreateWorld((3, 3, "TestDebugWall"));

        await Pair.Server.WaitPost(() =>
        {
            _pathfinding.DebugCaptureUntil = TimeSpan.Zero;
            _pathfinding.Explored.Clear();
        });

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(_pathfinding.IsCapturing, Is.False);
            Assert.That(_pathfinding.Explored, Is.Empty);
        });
    }

    /// <summary>
    /// The client asks for the data of the overlays that are on, and gets it; with the overlays off it asks for nothing.
    /// </summary>
    [Test]
    public async Task TestClientAsksForWhatIsOnAndStopsWhenItIsOff()
    {
        await CreateWorld((3, 3, "TestDebugWall"));

        await Pair.Client.WaitPost(() =>
        {
            _clientDebug.Set(PathDebugLayers.Mesh, true);
            _clientDebug.Set(PathDebugLayers.Cost, true);
            _clientDebug.Profile = "TestDebugLenient";
            _clientDebug.Refresh();
        });
        await Pair.RunTicksSync(30);

        await Pair.Client.WaitAssertion(() =>
        {
            Assert.That(_clientDebug.Layers, Is.EqualTo(PathDebugLayers.Mesh | PathDebugLayers.Cost));
            Assert.That(_clientDebug.Response, Is.Not.Null, "the server has answered");
            Assert.That(_clientDebug.Response!.Layers, Is.EqualTo(PathDebugLayers.Mesh | PathDebugLayers.Cost));
            Assert.That(_clientDebug.Response.Info, Does.Contain("mesh:"));
        });

        await Pair.Client.WaitPost(() =>
        {
            _clientDebug.Set(PathDebugLayers.Mesh, false);
            _clientDebug.Set(PathDebugLayers.Cost, false);
        });
        await Pair.RunTicksSync(5);

        await Pair.Client.WaitAssertion(() =>
        {
            Assert.That(_clientDebug.Layers, Is.EqualTo(PathDebugLayers.None));
            Assert.That(_clientDebug.Response, Is.Null, "nothing is kept when the overlays are off");
        });
    }
}
