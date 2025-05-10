using System.Linq;
using Content.Server.NPC;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.HTN.Preconditions.Farming;

public sealed partial class RefillableSolutionPrecondition : InvertiblePrecondition
{
    private SharedSolutionContainerSystem _solution;

    [DataField(required: true)]
    public string TargetKey;

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
        return blackboard.TryGetValue(TargetKey, out EntityUid? uid, EntityManager)
               && _solution.TryGetRefillableSolution(uid.Value, out _, out var solution)
               && solution.Contents.FirstOrDefault(x => x.Reagent.Prototype == Reagent) is { } reagent
               &&  (MoreThan != null && reagent.Quantity > MoreThan || LessThan != null && reagent.Quantity < LessThan);
    }
}
