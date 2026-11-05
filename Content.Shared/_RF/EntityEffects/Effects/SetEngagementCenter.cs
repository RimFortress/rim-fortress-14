using System.Numerics;
using Content.Shared._RF.NPC.Engagement.Components;
using Content.Shared._RF.NPC.Engagement.Prototypes;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;
using Content.Shared._RF.NPC.Groups.Systems;
using Content.Shared.EntityEffects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.EntityEffects.Effects;

/// <summary>
/// Puts the coordinates of the middle of the participants of the situations of a kind that the target owns into a key of its own
/// state, such as the place where the raiders have come, which they keep to while they wait. The participants read it from there,
/// see <c>Engagement/RaidForce/Owner/CampTarget</c>.
/// </summary>
public sealed partial class SetEngagementCenter : EntityEffectBase<SetEngagementCenter>
{
    /// <summary>
    /// The kind of the situations.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId<EngagementComponent> Engagement;

    /// <summary>
    /// The role of those whose middle it is, all the roles if it is not set.
    /// </summary>
    [DataField]
    public ProtoId<EngagementRolePrototype>? Role;

    /// <summary>
    /// The key.
    /// </summary>
    [DataField]
    public StateKey<EntityCoordinates> Key = "CampTarget";

    /// <summary>
    /// Does nothing if the key has a value already: the place is chosen once, and the participants do not drag it after them.
    /// </summary>
    [DataField]
    public bool OnlyIfNone = true;
}

public sealed partial class SetEngagementCenterEntityEffectsSystem : EntityEffectSystem<GoapComponent, SetEngagementCenter>
{
    [Dependency] private GroupsSystem _groups = null!;
    [Dependency] private SharedGoapSystem _goap = null!;
    [Dependency] private SharedTransformSystem _transform = null!;

    protected override void Effect(Entity<GoapComponent> ent, ref EntityEffectEvent<SetEngagementCenter> args)
    {
        var effect = args.Effect;

        if (effect.OnlyIfNone && _goap.TryGetValue(ent.Comp.State, effect.Key, out _))
            return;

        var sum = Vector2.Zero;
        var count = 0;
        var map = MapId.Nullspace;

        foreach (var member in _groups.GetMembers(ent, effect.Engagement, effect.Role))
        {
            if (TerminatingOrDeleted(member))
                continue;

            sum += _transform.GetWorldPosition(member);
            map = Transform(member).MapID;
            count++;
        }

        if (count == 0)
            return;

        _goap.SetValue(ent.Comp.State, effect.Key, _transform.ToCoordinates(new MapCoordinates(sum / count, map)));
    }
}
