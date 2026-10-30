#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server._RF.NPC.Pathfinding.Systems;
using Content.Server.Parallax;
using Content.Shared._RF.NPC.Pathfinding;
using Content.Shared._RF.Parallax.Fog;
using Content.Shared.Parallax.Biomes;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._RF.NPC.Pathfinding;

/// <summary>
/// The navigation map of a world that the biome makes: what the biome puts on the ground is where an agent cannot go,
/// whether the map was built before the biome has loaded the place or after.
/// </summary>
public sealed class RfPathfindingBiomeTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: goapCompound
  id: TestBiomeRoot
  tasks:
  - preconditions: []
    action: !type:TestNodeMove
    effects:
      Arrived: true

- type: entity
  id: TestBiomeAgent
  components:
  - type: Goap
    rootTask: TestBiomeRoot
  - type: PathProfiles
    profiles:
    - Walker
  - type: Physics
    bodyType: KinematicController
  - type: Fixtures
    fixtures:
      fix1:
        shape:
          !type:PhysShapeCircle
          radius: 0.35
        density: 50
        mask:
        - MobMask
        layer:
        - MobLayer
  - type: InputMover
  - type: MobMover
  - type: MovementSpeedModifier
";

    [SidedDependency(Side.Server)] private readonly IEntityManager _entMan = default!;
    [SidedDependency(Side.Server)] private readonly IPrototypeManager _proto = default!;
    [SidedDependency(Side.Server)] private readonly SharedMapSystem _maps = default!;
    [SidedDependency(Side.Server)] private readonly SharedTransformSystem _transform = default!;
    [SidedDependency(Side.Server)] private readonly BiomeSystem _biome = default!;
    [SidedDependency(Side.Server)] private readonly RfPathfindingSystem _pathfinding = default!;

    [Test]
    public async Task TestWallsOfTheBiomeAreBlocked([Values(true, false)] bool builtBeforeLoading)
    {
        EntityUid map = default;
        EntityUid loader = default;

        await Pair.Server.WaitPost(() =>
        {
            map = _maps.CreateMap(out var mapId);
            _biome.EnsurePlanet(map, _proto.Index<BiomeTemplatePrototype>("RfLowDesert"), 1234);
            loader = _entMan.SpawnEntity(null, new MapCoordinates(new Vector2(40f, 40f), mapId));
            _entMan.EnsureComponent<FogOfWarClearerComponent>(loader);
        });

        if (builtBeforeLoading)
        {
            // The map is built for the place first, from what the biome would create.
            await Pair.Server.WaitPost(() =>
            {
                var grid = new Entity<MapGridComponent>(map, _entMan.GetComponent<MapGridComponent>(map));

                for (var x = 0; x < 8; x++)
                {
                    for (var y = 0; y < 8; y++)
                    {
                        _pathfinding.EnsureChunk(grid, new Vector2i(x, y));
                    }
                }
            });
        }

        await Pair.RunTicksSync(120);

        var checkedWalls = 0;
        var missed = new List<string>();

        await Pair.Server.WaitPost(() =>
        {
            var grid = new Entity<MapGridComponent>(map, _entMan.GetComponent<MapGridComponent>(map));
            var query = _entMan.AllEntityQueryEnumerator<PhysicsComponent, TransformComponent, MetaDataComponent>();

            while (query.MoveNext(out var uid, out var body, out var xform, out var meta))
            {
                if (meta.EntityPrototype?.ID.StartsWith("WallRock") != true || xform.GridUid != map)
                    continue;

                var tile = _maps.CoordinatesToTile(map, grid.Comp, xform.Coordinates);
                var chunkOrigin = SharedMapSystem.GetChunkIndices(tile, 8);
                var chunk = _pathfinding.EnsureChunk(grid, chunkOrigin);

                if (chunk == null)
                    continue;

                var local = tile - chunkOrigin * 8;
                var polys = chunk.GetPolys(RfPathfindingChunk.TileIndex(local.X, local.Y));
                var blocked = polys.Count > 0 && polys.All(p => p.Data.CollisionMask != 0 || p.Data.CollisionLayer != 0);
                checkedWalls++;

                if (!blocked)
                    missed.Add($"{meta.EntityPrototype.ID} at {tile}, layer {body.CollisionLayer}, polys {polys.Count}");
            }
        });

        TestContext.Out.WriteLine($"MEASURE biome (built before: {builtBeforeLoading}): walls {checkedWalls}, not blocked {missed.Count}");

        foreach (var line in missed.Take(10))
        {
            TestContext.Out.WriteLine("MISSED " + line);
        }

        Assert.That(checkedWalls, Is.GreaterThan(0), "the biome must have made walls");
        Assert.That(missed, Is.Empty, "every wall of the biome must be in the navigation map");
    }

    /// <summary>
    /// The way around a wall that is next to the target, in the world the biome makes.
    /// </summary>
    [Test]
    public async Task TestAgentsGoAroundWallsOfTheBiome()
    {
        EntityUid map = default;
        EntityUid loader = default;

        await Pair.Server.WaitPost(() =>
        {
            map = _maps.CreateMap(out var mapId);
            _biome.EnsurePlanet(map, _proto.Index<BiomeTemplatePrototype>("RfLowDesert"), 1234);
            loader = _entMan.SpawnEntity(null, new MapCoordinates(new Vector2(40f, 40f), mapId));
            _entMan.EnsureComponent<FogOfWarClearerComponent>(loader);
        });

        await Pair.RunTicksSync(120);

        // Walls that have a free tile on both sides, one tile of them in a row.
        var cases = new List<(Vector2i From, Vector2i To)>();
        var closed = new List<(Vector2i From, Vector2i To)>();

        await Pair.Server.WaitPost(() =>
        {
            var grid = _entMan.GetComponent<MapGridComponent>(map);
            var walls = new HashSet<Vector2i>();
            var query = _entMan.AllEntityQueryEnumerator<PhysicsComponent, TransformComponent, MetaDataComponent>();

            while (query.MoveNext(out _, out _, out var xform, out var meta))
            {
                if (meta.EntityPrototype?.ID.StartsWith("WallRock") == true && xform.GridUid == map)
                    walls.Add(_maps.CoordinatesToTile(map, grid, xform.Coordinates));
            }

            bool Free(Vector2i tile) => !walls.Contains(tile) && !_maps.GetTileRef(map, grid, tile).Tile.IsEmpty;

            // How far the tile is to walk, null if it cannot be reached in a reasonable way.
            int? Distance(Vector2i from, Vector2i to)
            {
                var seen = new Dictionary<Vector2i, int> { [from] = 0 };
                var queue = new Queue<Vector2i>();
                queue.Enqueue(from);

                while (queue.Count > 0)
                {
                    var tile = queue.Dequeue();

                    if (tile == to)
                        return seen[tile];

                    if (seen[tile] > 400)
                        continue;

                    foreach (var step in new[] { new Vector2i(1, 0), new Vector2i(-1, 0), new Vector2i(0, 1), new Vector2i(0, -1) })
                    {
                        var next = tile + step;

                        if (seen.ContainsKey(next) || !Free(next))
                            continue;

                        seen[next] = seen[tile] + 1;
                        queue.Enqueue(next);
                    }
                }

                return null;
            }

            // Walls with floor on both sides that can be got around, and the ones that cannot.
            foreach (var wall in walls.OrderBy(w => w.X).ThenBy(w => w.Y))
            {
                var south = wall + new Vector2i(0, -1);
                var north = wall + new Vector2i(0, 1);

                if (!Free(south) || !Free(north))
                    continue;

                var way = Distance(south, north);
                TestContext.Out.WriteLine($"CASE {south} -> {north}: way {way}");

                if (way is >= 3 and <= 40)
                    cases.Add((south, north));
                else if (way == null && closed.Count < 1)
                    closed.Add((south, north));

                if (cases.Count >= 6)
                    break;
            }
        });

        Assert.That(cases, Is.Not.Empty, "the biome must have a wall with floor on both sides");

        var failures = new List<string>();

        foreach (var (from, to) in cases)
        {
            EntityUid agent = default;
            await Pair.Server.WaitPost(() => agent = _entMan.SpawnAtPosition("TestBiomeAgent", new EntityCoordinates(map, from.X + 0.5f, from.Y + 0.5f)));
            await Pair.RunTicksSync(3);

            await Pair.Server.WaitPost(() =>
            {
                var grid = new Entity<MapGridComponent>(map, _entMan.GetComponent<MapGridComponent>(map));
                var wallTile = from + new Vector2i(0, 1);
                var chunkOrigin = SharedMapSystem.GetChunkIndices(wallTile, 8);
                var chunk = _pathfinding.EnsureChunk(grid, chunkOrigin)!;
                var local = wallTile - chunkOrigin * 8;

                foreach (var poly in chunk.GetPolys(RfPathfindingChunk.TileIndex(local.X, local.Y)))
                {
                    TestContext.Out.WriteLine($"POLY {wallTile}: box {poly.Box} layer {poly.Data.CollisionLayer} mask {poly.Data.CollisionMask} influences {poly.Data.Facts.InfluenceCount}");
                }

                var ag = new Entity<Content.Shared._RF.NPC.Pathfinding.Components.PathProfilesComponent, Content.Shared._RF.NPC.GOAP.Components.GoapComponent>(
                    agent,
                    _entMan.GetComponent<Content.Shared._RF.NPC.Pathfinding.Components.PathProfilesComponent>(agent),
                    _entMan.GetComponent<Content.Shared._RF.NPC.GOAP.Components.GoapComponent>(agent));
                Assert.That(_pathfinding.BeginRequest(ag, "Walker", "Travel"), Is.True);
                var profile = _proto.Index<Content.Shared._RF.NPC.Pathfinding.Prototypes.PathQueryPrototype>("Travel");
                var job = new PathJob(0.05, ag, new EntityCoordinates(map, from.X + 0.5f, from.Y + 0.5f), new EntityCoordinates(map, from.X + 0.5f, from.Y + 2.5f), 0.5f, profile.Algorithm, _pathfinding);
                var queue = new Robust.Shared.CPUJob.JobQueues.Queues.JobQueue();
                queue.EnqueueJob(job);
                for (var k = 0; k < 100000 && job.Status != Robust.Shared.CPUJob.JobQueues.JobStatus.Finished; k++) queue.Process();
                TestContext.Out.WriteLine($"SEARCH {from}: {job.Result.Status} {profile.Algorithm.GetType().Name} nodes {string.Join(" ", job.Result.Path.Select(n => $"({n.Box.Center.X:F1},{n.Box.Center.Y:F1})"))}");
                foreach (var n in job.Result.Path)
                {
                    var c = _pathfinding.EnsureChunk(grid, n.Chunk)!;
                    var pl = c.GetPolys(n.Tile).FirstOrDefault(q => q.Box == n.Box);
                    if (pl != null)
                        TestContext.Out.WriteLine($"  NODE {n.Box.Center}: eval modifier {_pathfinding.EvaluateNode(ag, pl.Data.Facts, (30 & pl.Data.CollisionLayer) != 0 || (65 & pl.Data.CollisionMask) != 0).Modifier}");
                }

                if (from == new Vector2i(25, 33))
                {
                    for (var yy = 36; yy >= 31; yy--)
                    {
                        var row = "";
                        for (var xx = 22; xx <= 28; xx++)
                        {
                            var t = new Vector2i(xx, yy);
                            var co = SharedMapSystem.GetChunkIndices(t, 8);
                            var ch = _pathfinding.EnsureChunk(grid, co)!;
                            var lo = t - co * 8;
                            var ps = ch.GetPolys(RfPathfindingChunk.TileIndex(lo.X, lo.Y));
                            row += ps.Count == 0 ? " [none]" : $" [{ps.Count}p L{ps[0].Data.CollisionLayer} S{(ps[0].Data.IsFreeSpace ? 1 : 0)} n{ps[0].Neighbors.Count}]";
                        }
                        TestContext.Out.WriteLine($"ROW {yy}:{row}");
                    }
                }

                var fix = _entMan.GetComponent<Robust.Shared.Physics.FixturesComponent>(agent).Fixtures["fix1"];
                TestContext.Out.WriteLine($"AGENT layer {fix.CollisionLayer} mask {fix.CollisionMask}");
            });

            var result = Content.Shared._RF.NPC.GOAP.GoapActionResult.Continuing;
            var position = Vector2.Zero;

            for (var i = 0; i < 1200 && result == Content.Shared._RF.NPC.GOAP.GoapActionResult.Continuing; i++)
            {
                await Pair.RunTicksSync(1);
                await Pair.Server.WaitPost(() =>
                {
                    result = _entMan.System<Content.Server._RF.NPC.Steering.Systems.RfSteeringSystem>().Move(
                        new Entity<Content.Shared._RF.NPC.GOAP.Components.GoapComponent>(
                            agent,
                            _entMan.GetComponent<Content.Shared._RF.NPC.GOAP.Components.GoapComponent>(agent)),
                        new EntityCoordinates(map, to.X + 0.5f, to.Y + 0.5f),
                        0.5f);
                    position = _transform.GetWorldPosition(agent);

                    if (i % 40 == 0 && i < 400 && _entMan.TryGetComponent(agent, out Content.Server._RF.NPC.Steering.Components.RfSteeringComponent? sc))
                    {
                        var st = _entMan.GetComponent<Content.Server._RF.NPC.Steering.Components.RfSteererComponent>(agent);
                        TestContext.Out.WriteLine($"TRACE {from}: {i} pos {position} idx {sc.Index}/{sc.Nodes.Count} des {sc.Desired} {sc.Status} repaths {st.Repaths} partial {st.Partial} end {sc.End} nodes {string.Join(" ", sc.Nodes.Select(n => $"({n.Center.X:F1},{n.Center.Y:F1})"))}");
                    }
                });
            }

            TestContext.Out.WriteLine($"MEASURE wall {from} -> {to}: {result} at {position}");

            if (result != Content.Shared._RF.NPC.GOAP.GoapActionResult.Finished)
                failures.Add($"{from} -> {to}: {result} at {position}");

            await Pair.Server.WaitPost(() => _entMan.DeleteEntity(agent));
        }

        Assert.That(failures, Is.Empty);

        // A place that cannot be got to, the wall is the only way: the agent does not go to the wall to push through it.
        foreach (var (from, to) in closed)
        {
            EntityUid agent = default;
            await Pair.Server.WaitPost(() => agent = _entMan.SpawnAtPosition("TestBiomeAgent", new EntityCoordinates(map, from.X + 0.5f, from.Y + 0.5f)));
            await Pair.RunTicksSync(3);

            var result = Content.Shared._RF.NPC.GOAP.GoapActionResult.Continuing;
            var ticks = 0;

            for (; ticks < 400 && result == Content.Shared._RF.NPC.GOAP.GoapActionResult.Continuing; ticks++)
            {
                await Pair.RunTicksSync(1);
                await Pair.Server.WaitPost(() =>
                {
                    result = _entMan.System<Content.Server._RF.NPC.Steering.Systems.RfSteeringSystem>().Move(
                        new Entity<Content.Shared._RF.NPC.GOAP.Components.GoapComponent>(
                            agent,
                            _entMan.GetComponent<Content.Shared._RF.NPC.GOAP.Components.GoapComponent>(agent)),
                        new EntityCoordinates(map, to.X + 0.5f, to.Y + 0.5f),
                        0.5f);
                });
            }

            var moved = Vector2.Zero;
            await Pair.Server.WaitPost(() => moved = _transform.GetWorldPosition(agent) - new Vector2(from.X + 0.5f, from.Y + 0.5f));
            TestContext.Out.WriteLine($"MEASURE closed {from} -> {to}: {result} after {ticks} ticks, moved {moved.Length():F2}");

            Assert.That(result, Is.EqualTo(Content.Shared._RF.NPC.GOAP.GoapActionResult.Failed));
            Assert.That(moved.Length(), Is.LessThan(0.5f), "the agent must not go anywhere if there is no way");
            await Pair.Server.WaitPost(() => _entMan.DeleteEntity(agent));
        }
    }
}
