using Content.Shared._RF.NPC.Engagement.Components;
using Content.Shared._RF.NPC.Engagement.Prototypes;
using Content.Shared._RF.NPC.Groups.Systems;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.EntityEffects.Effects;

/// <summary>
/// Makes the user of the effect take a role in a group (<see cref="GroupsSystem"/>) that the target owns: the groups of the kind are
/// tried one by one for a place in one of the roles, and if there is none in any, a new one is started, and the target becomes its
/// owner. It is how the groups fill up: a group takes as many as its roles can have, and the next one goes to another. The
/// roles have to be ones that can be taken by force.
/// </summary>
public sealed partial class PlaceInGroup : EntityEffectBase<PlaceInGroup>
{
    /// <summary>
    /// The kind of the group.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId<EngagementComponent> Group;

    /// <summary>
    /// The roles that are taken, the first that has a place. Any role that is fit if it is empty.
    /// </summary>
    [DataField]
    public List<ProtoId<EngagementRolePrototype>> Roles = new();
}

public sealed partial class PlaceInGroupEntityEffectsSystem : EntityEffectSystem<MetaDataComponent, PlaceInGroup>
{
    [Dependency] private GroupsSystem _groups = null!;

    protected override void Effect(Entity<MetaDataComponent> ent, ref EntityEffectEvent<PlaceInGroup> args)
    {
        if (args.User is { } user)
            _groups.Place(ent, args.Effect.Group, user, args.Effect.Roles);
    }
}
