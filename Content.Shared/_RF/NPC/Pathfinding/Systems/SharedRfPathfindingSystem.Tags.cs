using System.Numerics;
using Content.Shared._RF.NPC.Pathfinding.Components;
using Content.Shared._RF.NPC.Pathfinding.Prototypes;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._RF.NPC.Pathfinding.Systems;

// Path tags of entities.
public abstract partial class SharedRfPathfindingSystem
{
    [Dependency] private IGameTiming _timing = null!;
    [Dependency] private SharedTransformSystem _transform = null!;
    [Dependency] private EntityQuery<RfGridPathfindingComponent> _gridQuery;

    /// <summary>
    /// How long to wait before the navigation map starts to update after the first change.
    /// </summary>
    private static readonly TimeSpan UpdateCooldown = TimeSpan.FromSeconds(0.45);

    [SubscribeLocalEvent]
    private void OnPathTagsStartup(Entity<PathTagsComponent> ent, ref ComponentStartup args)
        => MarkDirty(ent, ent.Comp);

    [SubscribeLocalEvent]
    private void OnPathTagsShutdown(Entity<PathTagsComponent> ent, ref ComponentShutdown args)
        => MarkDirty(ent, ent.Comp);

    [SubscribeLocalEvent]
    private void OnPathTagsMove(Entity<PathTagsComponent> ent, ref MoveEvent args)
    {
        MarkDirty(ent, ent.Comp);

        // The place the entity has left, if it was lying directly on a grid.
        if (_gridQuery.HasComp(args.OldPosition.EntityId))
            MarkDirty(args.OldPosition.EntityId, args.OldPosition.Position, GetInfluenceRadius(ent.Comp, null));
    }

    /// <summary>
    /// Does the entity have the path tag.
    /// </summary>
    /// <param name="ent">The entity.</param>
    /// <param name="tag">Path tag.</param>
    [PublicAPI, Pure]
    public bool HasTag(Entity<PathTagsComponent?> ent, ProtoId<PathTagPrototype> tag)
        => Resolve(ent, ref ent.Comp, false) && ent.Comp.Tags.Contains(tag);

    /// <summary>
    /// Gives the entity the path tag. The navigation map is updated by the system itself.
    /// </summary>
    /// <param name="ent">The entity, the component is added if it does not have one.</param>
    /// <param name="tag">Path tag.</param>
    /// <returns>True, if the entity did not have the tag; otherwise, false.</returns>
    [PublicAPI]
    public bool AddTag(Entity<PathTagsComponent?> ent, ProtoId<PathTagPrototype> tag)
    {
        ent.Comp ??= EnsureComp<PathTagsComponent>(ent);

        if (!ent.Comp.Tags.Add(tag))
            return false;

        MarkDirty(ent, ent.Comp, tag);
        return true;
    }

    /// <summary>
    /// Takes the path tag away from the entity. The navigation map is updated by the system itself.
    /// </summary>
    /// <param name="ent">The entity.</param>
    /// <param name="tag">Path tag.</param>
    /// <returns>True, if the entity had the tag; otherwise, false.</returns>
    [PublicAPI]
    public bool RemoveTag(Entity<PathTagsComponent?> ent, ProtoId<PathTagPrototype> tag)
    {
        if (!Resolve(ent, ref ent.Comp, false) || !ent.Comp.Tags.Remove(tag))
            return false;

        MarkDirty(ent, ent.Comp, tag);
        return true;
    }

    /// <summary>
    /// Marks chunks around the entity that its tags influence as the ones to rebuild.
    /// </summary>
    /// <param name="uid">The entity.</param>
    /// <param name="comp">Its tags.</param>
    /// <param name="changed">The tag that has just been added or removed, which may not be in the component.</param>
    private void MarkDirty(EntityUid uid, PathTagsComponent comp, ProtoId<PathTagPrototype>? changed = null)
    {
        var xform = Transform(uid);

        if (xform.GridUid is not { } grid)
            return;

        var position = Vector2.Transform(_transform.GetWorldPosition(xform), _transform.GetInvWorldMatrix(grid));
        MarkDirty(grid, position, GetInfluenceRadius(comp, changed));
    }

    /// <summary>
    /// Marks the chunks of a grid that fall within a radius of a point as the ones to rebuild.
    /// </summary>
    /// <param name="grid">The grid.</param>
    /// <param name="position">The point in the grid's coordinates.</param>
    /// <param name="radius">Radius in tiles.</param>
    private void MarkDirty(EntityUid grid, Vector2 position, float radius)
    {
        if (!_gridQuery.TryComp(grid, out var comp))
            return;

        var now = _timing.CurTime;

        if (comp.NextUpdate < now && !MetaData(grid).EntityPaused)
            comp.NextUpdate = now + UpdateCooldown;

        var min = position - new Vector2(radius);
        var max = position + new Vector2(radius);
        var minChunk = new Vector2i((int) MathF.Floor(min.X / ChunkSize), (int) MathF.Floor(min.Y / ChunkSize));
        var maxChunk = new Vector2i((int) MathF.Floor(max.X / ChunkSize), (int) MathF.Floor(max.Y / ChunkSize));

        for (var x = minChunk.X; x <= maxChunk.X; x++)
        {
            for (var y = minChunk.Y; y <= maxChunk.Y; y++)
            {
                comp.DirtyChunks.Add(new Vector2i(x, y));
            }
        }
    }

    /// <summary>
    /// Returns the largest radius within which the tags influence nodes.
    /// </summary>
    private float GetInfluenceRadius(PathTagsComponent comp, ProtoId<PathTagPrototype>? changed)
    {
        var radius = 0f;

        foreach (var tag in comp.Tags)
        {
            if (TryGetTagIndex(tag, out var index))
                radius = MathF.Max(radius, GetTagRadius(index));
        }

        if (changed is { } other && TryGetTagIndex(other, out var changedIndex))
            radius = MathF.Max(radius, GetTagRadius(changedIndex));

        return radius;
    }
}
