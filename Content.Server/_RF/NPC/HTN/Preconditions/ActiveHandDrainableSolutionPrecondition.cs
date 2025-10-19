using System.Linq;
using Content.Server.NPC;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.HTN.Preconditions;

public sealed partial class ActiveHandDrainableSolutionPrecondition : InvertiblePrecondition
{
    private SharedSolutionContainerSystem _solution;

    [DataField(required: true)]
    public ProtoId<ReagentPrototype> Reagent;

    [DataField]
    public float? MoreThan;

    [DataField]
    public float? LessThan;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _solution = sysManager.GetEntitySystem<SharedSolutionContainerSystem>();
    }

    public override bool IsMetInvertible(NPCBlackboard blackboard)
    {
        return blackboard.TryGetValue<EntityUid>(NPCBlackboard.ActiveHandEntity, out var heldEntity, EntityManager)
               && _solution.TryGetDrainableSolution(heldEntity, out _, out var solution)
               && solution.Contents.FirstOrDefault(x => x.Reagent.Prototype == Reagent) is { } reagent
               &&  (MoreThan != null && reagent.Quantity > MoreThan || LessThan != null && reagent.Quantity < LessThan);
    }
}
