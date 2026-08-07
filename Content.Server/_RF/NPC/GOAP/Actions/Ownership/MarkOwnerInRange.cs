using Content.Server._RF.NPC.GOAP.Systems;
using Content.Shared._RF.NPC;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared.Item;
using Content.Shared.Whitelist;

namespace Content.Server._RF.NPC.GOAP.Actions.Ownership;

/// <summary>
/// Marks entities within a certain radius of the agent as owned by the agent's owners.
/// </summary>
public sealed partial class MarkOwnerInRange : BaseGoapAction<MarkOwnerInRange>
{
    /// <summary>
    /// Radius.
    /// </summary>
    [DataField]
    public float Range = 1f;

    /// <summary>
    /// Whitelist for entities within a radius.
    /// </summary>
    [DataField]
    public EntityWhitelist? Whitelist;
}

public sealed class MarkOwnerInRangeActionSystem : GoapActionSystem<MarkOwnerInRange>
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly OwnershipSystem _ownership = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;

    protected override bool ActionStartup(Entity<GoapComponent> ent, MarkOwnerInRange action)
    {
        var owners = _ownership.GetOwners(ent);

        if (owners.Count == 0)
        {
            CreateDump(ent, action, "agent has 0 owners");
            return true;
        }

        var entities = _lookup.GetEntitiesInRange(
            Goap.GetValue(ent.Comp.State, GoapState.OwnerCoordinates),
            action.Range);

        foreach (var entity in entities)
        {
            if (_whitelist.IsWhitelistPassOrNull(action.Whitelist, entity))
                _ownership.AddOwners(entity, owners);
        }

        CreateDump(ent,
            action,
            $"added owners ({string.Join(", ", owners)}) to the targets ({string.Join(", ", entities)})");
        return true;
    }
}
