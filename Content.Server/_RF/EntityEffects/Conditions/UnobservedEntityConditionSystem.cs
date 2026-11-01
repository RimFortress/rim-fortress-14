using Content.Shared._RF.EntityEffects.Conditions;
using Content.Shared.EntityConditions;
using Content.Shared.Parallax.Biomes;
using Robust.Shared.Map.Components;

namespace Content.Server._RF.EntityEffects.Conditions;

public sealed partial class UnobservedEntityConditionSystem : EntityConditionSystem<TransformComponent, Unobserved>
{
    [Dependency] private SharedBiomeSystem _biome = null!;
    [Dependency] private SharedMapSystem _maps = null!;

    [Dependency] private EntityQuery<BiomeComponent> _biomeQuery;
    [Dependency] private EntityQuery<MapGridComponent> _gridQuery;

    protected override void Condition(Entity<TransformComponent> ent, ref EntityConditionEvent<Unobserved> args)
    {
        // A world without a biome has nothing that is not loaded.
        if (ent.Comp.GridUid is not { } grid
            || !_biomeQuery.TryComp(grid, out var biome)
            || !_gridQuery.TryComp(grid, out var comp))
        {
            args.Result = false;
            return;
        }

        var tile = _maps.CoordinatesToTile(grid, comp, ent.Comp.Coordinates);
        var chunk = SharedMapSystem.GetChunkIndices(tile, SharedBiomeSystem.ChunkSize) * SharedBiomeSystem.ChunkSize;
        args.Result = !_biome.IsChunkLoaded((grid, biome), chunk);
    }
}
