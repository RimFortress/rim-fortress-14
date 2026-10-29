#nullable enable
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server._RF.NPC.Pathfinding.Systems;
using Content.Shared._RF.NPC.Pathfinding;
using Content.Shared._RF.NPC.Pathfinding.Components;
using Content.Shared._RF.NPC.Pathfinding.Systems;
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
            Assert.That(near.Length, Is.GreaterThan(1), "points at different distances must not be merged");

            foreach (var poly in near)
            {
                Assert.That(_pathfinding.HasTag(poly.Data.Facts, "TestLava"), Is.False);
                Assert.That(_pathfinding.GetInfluence(poly.Data.Facts, "TestLava"), Is.GreaterThan(0f));
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
}
