using JetBrains.Annotations;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Player;

namespace Content.Shared._RF.Parallax.Fog;

public abstract class SharedFogOfWarSystem : EntitySystem
{
    [Dependency] private readonly ISharedPlayerManager _player = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly SharedViewSubscriberSystem _viewSubscriber = default!;
    [Dependency] private readonly SharedEyeSystem _eye = default!;

    private float _viewSize;

    private const int ChunkSize = 8;

    public override void Initialize()
    {
        base.Initialize();

        _cfg.OnValueChanged(CVars.NetPvsPriorityRange,
            value => _viewSize = Math.Max(ChunkSize, value),
            true);
    }

    /// <summary>
    /// Adds the ability for an entity to dispel the fog of war at a certain distance
    /// </summary>
    /// <param name="ent">Entity</param>
    /// <param name="player">A player for whom the entity will dispel the fog</param>
    /// <param name="range">The radius at which the fog will dissipate</param>
    [PublicAPI]
    public void AddFogClearer(
        Entity<FogOfWarClearerComponent?, EyeComponent?> ent,
        EntityUid player,
        float? range = null)
    {
        if (_player.TryGetSessionByEntity(player, out var session))
            AddFogClearer(ent, session, range);
    }

    /// <summary>
    /// Adds the ability for an entity to dispel the fog of war at a certain distance
    /// </summary>
    /// <param name="ent">Entity</param>
    /// <param name="session">A player for whom the entity will dispel the fog</param>
    /// <param name="range">The radius at which the fog will dissipate</param>
    [PublicAPI]
    public void AddFogClearer(Entity<FogOfWarClearerComponent?, EyeComponent?> ent, ICommonSession session, float? range = null)
    {
        ent.Comp1 = EnsureComp<FogOfWarClearerComponent>(ent);
        _viewSubscriber.AddViewSubscriber(ent, session);
        SetClearerRange(ent, range ?? ent.Comp1.Range);
    }

    [PublicAPI]
    public void RemoveFogClearer(Entity<FogOfWarClearerComponent?> ent, EntityUid player)
    {
        if (_player.TryGetSessionByEntity(player, out var session))
            RemoveFogClearer(ent, session);
    }

    [PublicAPI]
    public void RemoveFogClearer(Entity<FogOfWarClearerComponent?> ent, ICommonSession session)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        _viewSubscriber.RemoveViewSubscriber(ent, session);
    }

    /// <summary>
    /// Sets the fog dissipation radius for this entity
    /// </summary>
    [PublicAPI]
    public void SetClearerRange(Entity<FogOfWarClearerComponent?, EyeComponent?> ent, float range)
    {
        if (!Resolve(ent, ref ent.Comp1) || !Resolve(ent, ref ent.Comp2))
            return;

        _eye.SetPvsScale(new(ent.Owner, ent.Comp2), 2 * range / _viewSize);

        ent.Comp1.Range = range;
        Dirty(ent, ent.Comp1);
    }

    [PublicAPI]
    public void SetEnabled(Entity<FogOfWarComponent?> ent, bool enabled)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        ent.Comp.Enabled = enabled;
        Dirty(ent);
    }

    [PublicAPI, Pure]
    public bool ChunkLoaded(Entity<FogOfWarComponent?> ent, Vector2i chunk)
        => Resolve(ent, ref ent.Comp) && ent.Comp.LoadedChunks.Contains(chunk);
}
