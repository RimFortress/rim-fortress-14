using System.Linq;
using Content.Server._RF.NPC.GOAP.Systems;
using Content.Shared._RF.NPC;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;

namespace Content.Server._RF.NPC.GOAP.Actions.Interaction;

/// <summary>
/// Marks the target entity owned by the agent's owners.
/// </summary>
public sealed partial class MarkOwner : BaseGoapAction<MarkOwner>
{
    /// <summary>
    /// Target entity.
    /// </summary>
    [DataField]
    public StateKey<EntityUid> TargetKey = "Target";
}

public sealed class MarkOwnerSystem : GoapActionSystem<MarkOwner>
{
    [Dependency] private readonly OwnershipSystem _ownership = default!;

    protected override float ActionCost(Entity<GoapComponent> ent, GoapState state, MarkOwner action) => 0f;

    protected override bool ActionStartup(Entity<GoapComponent> ent, MarkOwner action)
    {
        if (!TryGetValue(ent, action, action.TargetKey, out var target))
            return false;

        var owners = _ownership.GetOwners(ent);
        _ownership.AddOwners(target, owners);
        CreateDump(ent, action, $"added {string.Join(',', owners.Select(x => ToPrettyString(x)))} to owners of {ToPrettyString(target)}");
        return true;
    }
}
