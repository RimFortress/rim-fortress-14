#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server._RF.NPC.Steering.Systems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._RF.NPC.Pathfinding;

/// <summary>
/// What the agents of the game do with what the world has: the tiles and the things that are on them, with the profile
/// that the colonists have. Lava, liquid plasma and chasms are not gone through, the tiles next to the lava are gone by
/// less, water and ice are gone through if there is a reason.
/// </summary>
public sealed class RfPathfindingContentTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: goapCompound
  id: TestContentRoot
  tasks:
  - preconditions: []
    action: !type:TestNodeMove
    effects:
      Arrived: true

- type: entity
  id: TestContentAgent
  components:
  - type: Goap
    rootTask: TestContentRoot
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

    private const int Width = 30;
    private const int Height = 14;

    [SidedDependency(Side.Server)] private readonly IEntityManager _entMan = default!;
    [SidedDependency(Side.Server)] private readonly SharedMapSystem _maps = default!;
    [SidedDependency(Side.Server)] private readonly SharedTransformSystem _transform = default!;
    [SidedDependency(Side.Server)] private readonly ITileDefinitionManager _tileDefs = default!;

    private Entity<MapGridComponent> _grid;
    private EntityUid _agent;

    /// <summary>
    /// Makes a field of floor with the things on it and an agent at the left side of it.
    /// </summary>
    private async Task Setup(string? entity, IEnumerable<Vector2i> tiles, string? tile = null)
    {
        var map = await Pair.CreateTestMap();
        _grid = map.Grid;
        var places = tiles.ToList();

        await Pair.Server.WaitPost(() =>
        {
            for (var x = 0; x < Width; x++)
            {
                for (var y = 0; y < Height; y++)
                {
                    var type = tile != null && places.Contains(new Vector2i(x, y))
                        ? _tileDefs[tile].TileId
                        : (ushort) 1;

                    _maps.SetTile(_grid.Owner, _grid.Comp, new Vector2i(x, y), new Tile(type));
                }
            }

            if (entity != null)
            {
                foreach (var place in places)
                {
                    _entMan.SpawnAtPosition(entity, new EntityCoordinates(_grid, place.X + 0.5f, place.Y + 0.5f));
                }
            }

            _agent = _entMan.SpawnAtPosition("TestContentAgent", new EntityCoordinates(_grid, 2.5f, 6.5f));
        });

        await Pair.RunTicksSync(5);
    }

    private static IEnumerable<Vector2i> Strip(int x) => Enumerable.Range(0, Height).Select(y => new Vector2i(x, y));

    private static IEnumerable<Vector2i> Block(int x, int y, int size)
        => Enumerable.Range(x, size).SelectMany(i => Enumerable.Range(y, size).Select(j => new Vector2i(i, j)));

    /// <summary>
    /// Sends the agent to the other side of the field and returns how it went, and how close to the given tiles it was.
    /// </summary>
    private async Task<(GoapActionResult Result, Vector2 Position, float Closest)> Go(IReadOnlyCollection<Vector2i> keepAwayFrom)
    {
        var result = GoapActionResult.Continuing;
        var closest = float.MaxValue;
        var position = Vector2.Zero;

        for (var i = 0; i < 1500 && result == GoapActionResult.Continuing; i++)
        {
            await Pair.RunTicksSync(1);
            await Pair.Server.WaitPost(() =>
            {
                result = _entMan.System<RfSteeringSystem>().Move(
                    new Entity<GoapComponent>(_agent, _entMan.GetComponent<GoapComponent>(_agent)),
                    new EntityCoordinates(_grid, 26.5f, 6.5f),
                    0.4f);
                position = _transform.GetWorldPosition(_agent);

                // The distance from the agent to the edge of the nearest tile.
                foreach (var tile in keepAwayFrom)
                {
                    var dx = Math.Max(Math.Max(tile.X - position.X, 0f), position.X - tile.X - 1f);
                    var dy = Math.Max(Math.Max(tile.Y - position.Y, 0f), position.Y - tile.Y - 1f);
                    closest = Math.Min(closest, MathF.Sqrt(dx * dx + dy * dy));
                }
            });
        }

        return (result, position, closest);
    }

    /// <summary>
    /// Lava, liquid plasma and a chasm that cannot be got around are not gone through: the agent does not go at all.
    /// </summary>
    [Test]
    public async Task TestDoesNotGoThroughDeadlyTiles(
        [Values("FloorLavaEntity", "FloorLiquidPlasmaEntity", "FloorChasmEntity")] string entity)
    {
        await Setup(entity, Strip(14));

        var (result, position, _) = await Go(Array.Empty<Vector2i>());
        TestContext.Out.WriteLine($"MEASURE {entity} across the field: {result} at {position}");

        Assert.That(result, Is.EqualTo(GoapActionResult.Failed));
        Assert.That(position.X, Is.LessThan(5f), "the agent must not have started to walk to the lava");
    }

    /// <summary>
    /// The deadly tiles in the way are gone around, and the lava is gone by at a distance if there is room.
    /// </summary>
    [Test]
    public async Task TestGoesAroundDeadlyTiles([Values("FloorLavaEntity", "FloorLiquidPlasmaEntity", "FloorChasmEntity")] string entity)
    {
        var block = Block(13, 5, 3).ToList();
        await Setup(entity, block);

        var (result, position, closest) = await Go(block);
        TestContext.Out.WriteLine($"MEASURE {entity} around a block: {result} at {position}, the closest {closest:F2}");

        Assert.That(result, Is.EqualTo(GoapActionResult.Finished));
        Assert.That(closest, Is.GreaterThan(0.3f), "the body of the agent must not have been over the tiles");
    }

    /// <summary>
    /// The tiles next to the lava cost more, so a path that has room goes farther from it than the path of the
    /// plasma does.
    /// </summary>
    [Test]
    public async Task TestKeepsAwayFromLava()
    {
        var block = Block(13, 5, 3).ToList();
        await Setup("FloorLavaEntity", block);
        var (_, _, lava) = await Go(block);
        await Pair.Server.WaitPost(() => _entMan.DeleteEntity(_agent));

        await Setup("FloorLiquidPlasmaEntity", block);
        var (_, _, plasma) = await Go(block);

        TestContext.Out.WriteLine($"MEASURE closest to the lava {lava:F2}, to the plasma {plasma:F2}");

        Assert.That(lava, Is.GreaterThan(plasma + 0.1f), "the lava is gone by farther than what has no heat");
        Assert.That(lava, Is.GreaterThan(0.8f), "the tile next to the lava is not walked on if it can be helped");
        Assert.That(plasma, Is.GreaterThan(0.3f), "the body of the agent is not over the plasma");
    }

    /// <summary>
    /// Water that cannot be got around is walked through.
    /// </summary>
    [Test]
    public async Task TestWalksThroughWaterIfThereIsNoWayAround()
    {
        await Setup("FloorWaterEntity", Strip(14));

        var (result, position, _) = await Go(Array.Empty<Vector2i>());
        TestContext.Out.WriteLine($"MEASURE water across the field: {result} at {position}");

        Assert.That(result, Is.EqualTo(GoapActionResult.Finished));
    }

    /// <summary>
    /// Water that is easy to go around is gone around, and a little ice is gone around only if it is no trouble.
    /// </summary>
    [Test]
    public async Task TestPrefersToGoAroundWaterAndNotIce([Values("FloorWaterEntity", "FloorIce")] string kind)
    {
        // A strip of three tiles across the middle of the field, the row of the agent is in it.
        var strip = Enumerable.Range(12, 3).SelectMany(x => Enumerable.Range(4, 5).Select(y => new Vector2i(x, y))).ToList();

        if (kind == "FloorIce")
            await Setup(null, strip, "FloorIce");
        else
            await Setup(kind, strip);

        var crossed = false;
        var result = GoapActionResult.Continuing;

        for (var i = 0; i < 1500 && result == GoapActionResult.Continuing; i++)
        {
            await Pair.RunTicksSync(1);
            await Pair.Server.WaitPost(() =>
            {
                result = _entMan.System<RfSteeringSystem>().Move(
                    new Entity<GoapComponent>(_agent, _entMan.GetComponent<GoapComponent>(_agent)),
                    new EntityCoordinates(_grid, 26.5f, 6.5f),
                    0.4f);

                var position = _transform.GetWorldPosition(_agent);
                crossed |= position.X is > 12.5f and < 14.5f && position.Y is > 4.2f and < 8.8f;
            });
        }

        TestContext.Out.WriteLine($"MEASURE {kind}: {result}, went over it: {crossed}");

        Assert.That(result, Is.EqualTo(GoapActionResult.Finished));

        // Three tiles of water cost more than going around it, three tiles of ice do not.
        Assert.That(crossed, Is.EqualTo(kind == "FloorIce"));
    }

    /// <summary>
    /// A place that is picked at random is picked by the profile of the agent, so it is one the agent can go to: the agent
    /// that is in a place that is closed by lava does not pick a place on the other side.
    /// </summary>
    [Test]
    public async Task TestRandomPlaceIsChosenByTheProfile()
    {
        // A square of lava around the agent, it is three tiles from it, and there is floor on the other side.
        var ring = new List<Vector2i>();

        for (var x = 8; x <= 14; x++)
        {
            for (var y = 3; y <= 9; y++)
            {
                if (x is 8 or 14 || y is 3 or 9)
                    ring.Add(new Vector2i(x, y));
            }
        }

        await Setup("FloorLavaEntity", ring);

        await Pair.Server.WaitPost(() => _transform.SetCoordinates(_agent, new EntityCoordinates(_grid, 11.5f, 6.5f)));
        await Pair.RunTicksSync(3);

        var system = _entMan.System<Content.Server._RF.NPC.Pathfinding.Systems.RfPathfindingSystem>();
        var inside = 0;

        for (var attempt = 0; attempt < 15; attempt++)
        {
            Content.Shared._RF.NPC.Pathfinding.PathSearchResult result = default;
            var found = false;

            await Pair.Server.WaitPost(() =>
            {
                var agent = new Entity<Content.Shared._RF.NPC.Pathfinding.Components.PathProfilesComponent, GoapComponent>(
                    _agent,
                    _entMan.GetComponent<Content.Shared._RF.NPC.Pathfinding.Components.PathProfilesComponent>(_agent),
                    _entMan.GetComponent<GoapComponent>(_agent));
                Assert.That(system.RequestPath(agent, "Wander", null, 9f), Is.True);
            });

            for (var i = 0; i < 100 && !found; i++)
            {
                await Pair.RunTicksSync(2);
                await Pair.Server.WaitPost(() =>
                {
                    var agent = new Entity<Content.Shared._RF.NPC.Pathfinding.Components.PathProfilesComponent, GoapComponent>(
                        _agent,
                        _entMan.GetComponent<Content.Shared._RF.NPC.Pathfinding.Components.PathProfilesComponent>(_agent),
                        _entMan.GetComponent<GoapComponent>(_agent));
                    found = system.TryGetResult(agent, out result, out _);
                });
            }

            Assert.That(found, Is.True);
            Assert.That(result.Status, Is.EqualTo(Content.Shared._RF.NPC.Pathfinding.PathSearchStatus.Path));

            var place = result.Path[^1].Box.Center;

            if (place.X is > 9f and < 14f && place.Y is > 4f and < 9f)
                inside++;

            await Pair.Server.WaitPost(() =>
                system.CancelPath(new Entity<Content.Shared._RF.NPC.Pathfinding.Components.PathProfilesComponent?>(
                    _agent,
                    _entMan.GetComponent<Content.Shared._RF.NPC.Pathfinding.Components.PathProfilesComponent>(_agent))));
        }

        Assert.That(inside, Is.EqualTo(15), "every place must be inside the lava, where the agent can go");
    }

    /// <summary>
    /// The same profile, different rules for different queries: the way goes through water, and a place to wander to is
    /// not chosen in it or on the other side of it.
    /// </summary>
    [Test]
    public async Task TestWaterIsForTheWayAndNotForWandering()
    {
        var strip = Enumerable.Range(5, 2).SelectMany(x => Strip(x)).ToList();
        await Setup("FloorWaterEntity", strip);

        // The way to the other side goes through the water.
        var (result, _, _) = await Go(Array.Empty<Vector2i>());
        Assert.That(result, Is.EqualTo(GoapActionResult.Finished));

        await Pair.Server.WaitPost(() => _transform.SetCoordinates(_agent, new EntityCoordinates(_grid, 2.5f, 6.5f)));
        await Pair.RunTicksSync(3);

        var system = _entMan.System<Content.Server._RF.NPC.Pathfinding.Systems.RfPathfindingSystem>();
        var beyond = 0;

        for (var attempt = 0; attempt < 15; attempt++)
        {
            Content.Shared._RF.NPC.Pathfinding.PathSearchResult place = default;
            var found = false;

            for (var i = 0; i < 100 && !found; i++)
            {
                await Pair.Server.WaitPost(() =>
                {
                    var agent = new Entity<Content.Shared._RF.NPC.Pathfinding.Components.PathProfilesComponent, GoapComponent>(
                        _agent,
                        _entMan.GetComponent<Content.Shared._RF.NPC.Pathfinding.Components.PathProfilesComponent>(_agent),
                        _entMan.GetComponent<GoapComponent>(_agent));

                    if (i == 0)
                        Assert.That(system.RequestPath(agent, "Wander", null, 9f), Is.True);

                    found = system.TryGetResult(agent, out place, out _);
                });

                await Pair.RunTicksSync(2);
            }

            Assert.That(found, Is.True);

            if (place.Path[^1].Box.Center.X > 4.9f)
                beyond++;

            await Pair.Server.WaitPost(() =>
                system.CancelPath(new Entity<Content.Shared._RF.NPC.Pathfinding.Components.PathProfilesComponent?>(
                    _agent,
                    _entMan.GetComponent<Content.Shared._RF.NPC.Pathfinding.Components.PathProfilesComponent>(_agent))));
        }

        Assert.That(beyond, Is.Zero, "a place to wander to is not in the water or behind it");
    }
}
