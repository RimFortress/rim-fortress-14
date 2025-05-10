using System.Linq;
using Content.Server.NPC;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Hands.Components;
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
        return blackboard.TryGetValue(NPCBlackboard.ActiveHand, out Hand? activeHand, EntityManager)
               && activeHand.HeldEntity != null
               && _solution.TryGetDrainableSolution(activeHand.HeldEntity.Value, out _, out var solution)
               && solution.Contents.FirstOrDefault(x => x.Reagent.Prototype == Reagent) is { } reagent
               &&  (MoreThan != null && reagent.Quantity > MoreThan || LessThan != null && reagent.Quantity < LessThan);
    }
}
