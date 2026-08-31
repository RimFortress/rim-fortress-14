using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;
using Content.Shared._RF.NPC.Systems;

namespace Content.Server._RF.NPC.GOAP.Actions.Ownership;

/// <summary>
/// Makes the target entity owned by the agent's owners.
/// </summary>
public sealed partial class MarkOwner : BaseGoapAction<MarkOwner>
{
    /// <summary>
    /// Target entity.
    /// </summary>
    [DataField(required: true)]
    public StateKey<EntityUid> TargetKey;
}

public sealed class MarkOwnerActionSystem : GoapActionSystem<MarkOwner>
{
    [Dependency] private readonly OwnershipSystem _ownership = default!;

    protected override float ActionCost(Entity<GoapComponent> ent, GoapState state, MarkOwner action) => 0f;

    protected override bool ActionStartup(Entity<GoapComponent> ent, MarkOwner action)
    {
        if (!TryGet(ent, action.TargetKey, out var target))
            return false;

        var owners = _ownership.GetOwners(ent);
        _ownership.AddOwnership(target, owners: owners);
        CreateDump($"added owners. Target: {ToPrettyString(target)}. Owners: {string.Join(", ", owners)}");
        return true;
    }
}
