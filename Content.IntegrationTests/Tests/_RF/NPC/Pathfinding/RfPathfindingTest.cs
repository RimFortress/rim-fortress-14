#nullable enable
using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server._RF.NPC.Pathfinding.Systems;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.Pathfinding;
using Content.Shared._RF.NPC.Pathfinding.Algorithms;
using Content.Shared._RF.NPC.Pathfinding.Components;
using Content.Shared._RF.NPC.Pathfinding.Systems;
using Robust.Shared.CPUJob.JobQueues;
using Robust.Shared.CPUJob.JobQueues.Queues;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._RF.NPC.Pathfinding;

[TestOf(typeof(SharedRfPathfindingSystem))]
public sealed class RfPathfindingTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: pathTag
  id: TestLava
  influence: 2

- type: entity
  id: TestPathWall
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
  id: TestPathLava
  components:
  - type: PathTags
    tags:
    - TestLava

- type: entity
  id: TestPathMarker

- type: goapCompound
  id: TestPathRoot
  tasks:
  - preconditions: []
    action: !type:MoveTo
    effects:
      Noop: true

- type: pathProfile
  id: TestWalker
  rules:
  - when:
    - !type:Blocked
    cost:
    - float: 10000

- type: entity
  id: TestPathAgent
  components:
  - type: Goap
    rootTask: TestPathRoot
  - type: PathProfiles
    profiles:
    - TestWalker
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

    [SidedDependency(Side.Server)] private readonly RfPathfindingSystem _pathfinding = default!;
    [SidedDependency(Side.Server)] private readonly SharedMapSystem _maps = default!;
    [SidedDependency(Side.Server)] private readonly IEntityManager _entMan = default!;

    private static RfPathPoly Poly(RfPathfindingChunk chunk, int x, int y)
        => chunk.GetPolys(RfPathfindingChunk.TileIndex(x, y))[0];

    /// <summary>
    /// A chunk built from the world: every kind of tile gets the data that the world has on it, the polys are linked,
    /// a chunk next to the map is built when a search reaches it and a chunk that is not used is removed.
    /// </summary>
    [Test]
    public async Task TestChunkLifecycle()
    {
        var map = await Pair.CreateTestMap();
        var grid = map.Grid;
        EntityUid marker = default;

        await Pair.Server.WaitPost(() =>
        {
            for (var x = 0; x < 8; x++)
            {
                for (var y = 0; y < 8; y++)
                {
                    _maps.SetTile(grid.Owner, grid.Comp, new Vector2i(x, y), new Tile(1));
                }
            }

            _entMan.SpawnAtPosition("TestPathWall", new EntityCoordinates(grid, 2.5f, 1.5f));
            _entMan.SpawnAtPosition("TestPathLava", new EntityCoordinates(grid, 5.5f, 1.5f));
            marker = _entMan.SpawnAtPosition("TestPathMarker", new EntityCoordinates(grid, 1.5f, 5.5f));
        });

        await Pair.RunTicksSync(5);

        RfPathPoly farPoly = default!;

        await Pair.Server.WaitAssertion(() =>
        {
            var chunk = _pathfinding.EnsureChunk(grid, Vector2i.Zero);
            Assert.That(chunk, Is.Not.Null);

            // An empty tile.
            farPoly = Poly(chunk!, 0, 0);
            Assert.That(farPoly.Data.IsFreeSpace, Is.True);
            Assert.That(farPoly.Data.CollisionMask, Is.EqualTo(0));
            Assert.That(farPoly.Neighbors.Count, Is.GreaterThanOrEqualTo(2), "polys must be linked to the polys next to them");

            // A wall that can be destroyed with a particular type of damage.
            var wall = Poly(chunk!, 2, 1);
            Assert.That(wall.Data.CollisionLayer, Is.Not.EqualTo(0));
            Assert.That(wall.Data.Facts.Destroy, Is.Not.Null);
            Assert.That(wall.Data.Facts.Destroy!.DamageDict["Structural"].Float(), Is.EqualTo(100f));

            // A tag is located on its tile.
            var lava = Poly(chunk!, 5, 1);
            Assert.That(_pathfinding.HasTag(lava.Data.Facts, "TestLava"), Is.True);
            Assert.That(_pathfinding.GetInfluence(lava.Data.Facts, "TestLava"), Is.GreaterThan(0f));

            // And influences the tiles close to it, the influence depends on the distance.
            var near = chunk!.GetPolys(RfPathfindingChunk.TileIndex(7, 1));
            Assert.That(near.Count, Is.GreaterThan(1), "points at different distances must not be merged");

            for (var i = 0; i < near.Count; i++)
            {
                Assert.That(_pathfinding.HasTag(near[i].Data.Facts, "TestLava"), Is.False);
                Assert.That(_pathfinding.GetInfluence(near[i].Data.Facts, "TestLava"), Is.GreaterThan(0f));
            }

            // A tile with the same tag far away is not influenced.
            Assert.That(_pathfinding.GetInfluence(farPoly.Data.Facts, "TestLava"), Is.EqualTo(0f));

            // The map is only a cache: the same chunk is returned while it exists.
            Assert.That(_pathfinding.EnsureChunk(grid, Vector2i.Zero), Is.SameAs(chunk));

            // A search that reaches the border of the chunk builds the next one, and the polys are linked.
            var border = Poly(chunk, 7, 6);
            Assert.That(_pathfinding.EnsureNeighbors(border), Is.EqualTo(1));
            Assert.That(_pathfinding.EnsureNeighbors(border), Is.EqualTo(0));
            Assert.That(border.Neighbors.Any(x => x.ChunkOrigin == new Vector2i(1, 0)), Is.True);
        });

        // A change of a tag marks the chunk, and the system rebuilds it by itself.
        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(_pathfinding.AddTag(marker, "TestLava"), Is.True);
            Assert.That(_entMan.GetComponent<RfGridPathfindingComponent>(grid).DirtyChunks, Does.Contain(Vector2i.Zero));
        });

        await Pair.RunTicksSync(60);

        await Pair.Server.WaitAssertion(() =>
        {
            var chunk = _entMan.GetComponent<RfGridPathfindingComponent>(grid).Chunks[Vector2i.Zero];
            Assert.That(_pathfinding.HasTag(Poly(chunk, 1, 5).Data.Facts, "TestLava"), Is.True);
            Assert.That(farPoly.IsValid(), Is.True, "polys that did not change must stay valid");
        });

        // Chunks that are not used are removed, and the polys are invalidated for those who still hold them.
        await Pair.Server.WaitPost(() =>
        {
            _entMan.GetComponent<RfGridPathfindingComponent>(grid).ChunkLifetime = TimeSpan.FromSeconds(1);
        });

        await Pair.RunTicksSync(900);

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(_entMan.GetComponent<RfGridPathfindingComponent>(grid).Chunks, Is.Empty);
            Assert.That(farPoly.IsValid(), Is.False);
        });
    }

    /// <summary>
    /// Creates a world of floor tiles and a path agent in its corner, which is out of the way of the searches.
    /// </summary>
    private async Task<(Entity<MapGridComponent> Grid, Entity<PathProfilesComponent, GoapComponent> Agent)> CreateWorld(
        int width,
        params (int X, int Y)[] walls)
    {
        var map = await Pair.CreateTestMap();
        var grid = map.Grid;
        EntityUid agent = default;

        await Pair.Server.WaitPost(() =>
        {
            for (var x = 0; x < width; x++)
            {
                for (var y = 0; y < 8; y++)
                {
                    _maps.SetTile(grid.Owner, grid.Comp, new Vector2i(x, y), new Tile(1));
                }
            }

            foreach (var (x, y) in walls)
            {
                _entMan.SpawnAtPosition("TestPathWall", new EntityCoordinates(grid, x + 0.5f, y + 0.5f));
            }

            agent = _entMan.SpawnAtPosition("TestPathAgent", new EntityCoordinates(grid, 0.5f, 7.5f));
        });

        await Pair.RunTicksSync(5);

        return (grid, new Entity<PathProfilesComponent, GoapComponent>(
            agent,
            _entMan.GetComponent<PathProfilesComponent>(agent),
            _entMan.GetComponent<GoapComponent>(agent)));
    }

    /// <summary>
    /// Runs the search the way a job queue does it.
    /// </summary>
    private PathSearchResult Search(
        Entity<MapGridComponent> grid,
        Entity<PathProfilesComponent, GoapComponent> agent,
        PathAlgorithm algorithm,
        Vector2 start,
        Vector2? target,
        float range = 0.4f,
        double maxTime = 0.05)
    {
        Assert.That(_pathfinding.BeginRequest(agent, "TestWalker"), Is.True);

        var job = new PathJob(
            maxTime,
            agent,
            new EntityCoordinates(grid, start),
            target == null ? null : new EntityCoordinates(grid, target.Value),
            range,
            algorithm,
            _pathfinding);
        var queue = new JobQueue();
        queue.EnqueueJob(job);

        for (var i = 0; i < 1_000_000 && job.Status != JobStatus.Finished; i++)
        {
            queue.Process();
        }

        Assert.That(job.Status, Is.EqualTo(JobStatus.Finished));
        return job.Result;
    }

    /// <summary>
    /// The path goes around a wall, which costs more than the detour, and the result does not depend on
    /// how often the job is suspended.
    /// </summary>
    [Test]
    public async Task TestAStarGoesAroundWalls()
    {
        var (grid, agent) = await CreateWorld(8, (3, 3));

        await Pair.Server.WaitAssertion(() =>
        {
            var target = new Vector2(6.5f, 3.5f);
            var result = Search(grid, agent, new AStar(), new Vector2(0.5f, 3.5f), target);

            Assert.That(result.Status, Is.EqualTo(PathSearchStatus.Path));
            Assert.That(result.Path, Is.Not.Empty);
            Assert.That(result.Path.Any(x => x.Box.Contains(new Vector2(3.5f, 3.5f))), Is.False, "the path must not go through the wall");
            Assert.That(result.Path[0].Box.Contains(new Vector2(0.5f, 3.5f)), Is.True);
            Assert.That(result.Path[^1].Box.Left, Is.GreaterThanOrEqualTo(5f), "the path must lead to the target");

            // A job that gives control back all the time must find the same path.
            var suspended = Search(grid, agent, new AStar(), new Vector2(0.5f, 3.5f), target, maxTime: 1e-7);
            Assert.That(suspended.Status, Is.EqualTo(PathSearchStatus.Path));
            Assert.That(suspended.Path, Is.EqualTo(result.Path));
        });
    }

    /// <summary>
    /// A search builds the chunks it reaches, but not more of them than it is allowed to.
    /// </summary>
    [Test]
    public async Task TestAStarBuildsChunksWithinTheLimit()
    {
        var (grid, agent) = await CreateWorld(32);

        await Pair.Server.WaitAssertion(() =>
        {
            var map = _entMan.GetComponent<RfGridPathfindingComponent>(grid);
            var start = new Vector2(0.5f, 3.5f);

            // Two chunks lie between the start and the target, and only one may be built.
            var limited = Search(grid, agent, new AStar { MaxChunks = 1 }, start, new Vector2(28.5f, 3.5f));
            Assert.That(limited.Status, Is.EqualTo(PathSearchStatus.PartialPath));
            Assert.That(map.Chunks.Keys, Does.Contain(new Vector2i(1, 0)), $"last node {limited.Path[^1]}, count {limited.Path.Count}");
            Assert.That(map.Chunks.ContainsKey(new Vector2i(2, 0)), Is.False);

            // The same search without the limit builds the rest and reaches the target.
            var result = Search(grid, agent, new AStar(), start, new Vector2(28.5f, 3.5f));
            Assert.That(result.Status, Is.EqualTo(PathSearchStatus.Path));
            Assert.That(result.Path[^1].Chunk, Is.EqualTo(new Vector2i(3, 0)));
            Assert.That(map.Chunks.ContainsKey(new Vector2i(2, 0)), Is.True);
        });
    }

    /// <summary>
    /// A wall costs a lot but can be passed, unless the search is not allowed to consider paths that expensive.
    /// </summary>
    [Test]
    public async Task TestAStarMaxCost()
    {
        var (grid, agent) = await CreateWorld(8, (4, 0), (4, 1), (4, 2), (4, 3), (4, 4), (4, 5), (4, 6), (4, 7));

        await Pair.Server.WaitAssertion(() =>
        {
            var start = new Vector2(0.5f, 3.5f);
            var target = new Vector2(7.5f, 3.5f);

            var through = Search(grid, agent, new AStar(), start, target);
            Assert.That(through.Status, Is.EqualTo(PathSearchStatus.Path));

            var limited = Search(grid, agent, new AStar { MaxCost = 5000f }, start, target);
            Assert.That(limited.Status, Is.EqualTo(PathSearchStatus.PartialPath));
            Assert.That(limited.Path[^1].Box.Right, Is.LessThanOrEqualTo(4f), "the path must stop in front of the wall");

            var none = Search(grid, agent, new AStar { MaxCost = 5000f, AllowPartial = false }, start, target);
            Assert.That(none.Status, Is.EqualTo(PathSearchStatus.NoPath));
        });
    }

    /// <summary>
    /// A search without a destination leads to a node within the range.
    /// </summary>
    [Test]
    public async Task TestBfsPicksNodeInRange()
    {
        var (grid, agent) = await CreateWorld(8);

        await Pair.Server.WaitAssertion(() =>
        {
            var start = new Vector2(3.5f, 3.5f);
            var result = Search(grid, agent, new Bfs { Range = 4f, MinRange = 3f, Pick = BfsPick.Farthest }, start, null);

            Assert.That(result.Status, Is.EqualTo(PathSearchStatus.Path));

            var distance = Vector2.Distance(start, result.Path[^1].Box.Center);
            Assert.That(distance, Is.InRange(3f, 4.01f));

            for (var i = 0; i < 20; i++)
            {
                var random = Search(grid, agent, new Bfs { Range = 4f, MinRange = 3f }, start, null);
                Assert.That(Vector2.Distance(start, random.Path[^1].Box.Center), Is.InRange(3f, 4.01f));
            }
        });
    }
}
