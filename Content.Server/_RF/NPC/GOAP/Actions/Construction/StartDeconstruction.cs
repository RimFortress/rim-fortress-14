using Content.Server.Construction;
using Content.Server.Construction.Components;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;

namespace Content.Server._RF.NPC.GOAP.Actions.Construction;

/// <summary>
/// Sets the target construction node for the target entity to deconstruction
/// </summary>
public sealed partial class StartDeconstruction : BaseGoapAction<StartDeconstruction>
{
    /// <summary>
    /// Target entity.
    /// </summary>
    [DataField(required: true)]
    public StateKey<EntityUid> TargetKey;
}

public sealed class StartDeconstructionAction : GoapActionSystem<StartDeconstruction>
{
    [Dependency] private readonly ConstructionSystem _construction = default!;

    protected override bool ActionStartup(Entity<GoapComponent> ent, StartDeconstruction action)
    {
        if (!TryGet(ent, action.TargetKey, out var target))
            return false;

        if (!TryComp(ent, out ConstructionComponent? comp))
        {
            ComponentNotFound<ConstructionComponent>();
            return false;
        }

        if (comp.TargetNode == comp.DeconstructionNode
            || comp.Node == comp.DeconstructionNode)
            return true;

        if (!_construction.SetPathfindingTarget(target, comp.DeconstructionNode, comp))
        {
            CreateDump($"failed to set pathfinding to node `{comp.DeconstructionNode}`");
            return false;
        }

        return true;
    }
}
