using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Systems;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Containers;

namespace Content.Server._RF.NPC.Search.Considerations;

/// <summary>
/// Evaluates a weapon/magazine based on the number of bullets in it.
/// </summary>
public sealed partial class Ammo : BaseSearchConsideration<Ammo>
{
    /// <summary>
    /// Whether the value will be normalized relative to the maximum number of bullets.
    /// </summary>
    [DataField]
    public bool Normalize = true;
}

public sealed class AmmoConsiderationSystem : NpcSearchConsiderationSystem<Ammo>
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeRescoreEvent<TakeAmmoEvent>();
        SubscribeRescoreEvent<GunComponent, EntInsertedIntoContainerMessage>();
    }

    protected override float GetScore(GoapState state, EntityUid target, Ammo con)
    {
        var ev = new GetAmmoCountEvent();
        RaiseLocalEvent(target, ref ev);

        if (!con.Normalize)
            return ev.Count;

        if (ev.Count == 0 || ev.Capacity == 0)
            return 0f;

        return (float)ev.Count / ev.Capacity;
    }
}
