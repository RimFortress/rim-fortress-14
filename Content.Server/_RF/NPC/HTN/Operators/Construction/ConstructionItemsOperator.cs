using System.Threading;
using System.Threading.Tasks;
using Content.Server._RF.Construction;
using Content.Server._RF.NPC.Systems;
using Content.Server.NPC;
using Content.Server.NPC.HTN.PrimitiveTasks;

namespace Content.Server._RF.NPC.HTN.Operators.Construction;

/// <summary>
/// Searches for all the items needed to build the target entity and stores them in a blackboard
/// </summary>
public sealed partial class ConstructionItemsOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entity = default!;

    private NpcConstructionSystem _construction;
    private NpcControlSystem _control;

    /// <summary>
    /// The key that contains the entity to be constructed
    /// </summary>
    [DataField(required: true)]
    public string TargetKey;

    /// <summary>
    /// A key that stores a list of all items needed for construction.
    /// </summary>
    [DataField]
    public string ResultKey = "ConstructionItems";

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _construction = sysManager.GetEntitySystem<NpcConstructionSystem>();
        _control = sysManager.GetEntitySystem<NpcControlSystem>();
    }

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard, CancellationToken cancelToken)
    {
        if (!blackboard.TryGetValue<EntityUid>(TargetKey, out var target, _entity))
            return (false, null);

        if (blackboard.ContainsKey(ResultKey))
            return (true, null);

        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        var items = _construction.GetConstructionItems(target, owner, out var reason);

        if (items == null)
        {
            _control.AddFailReason(owner, reason);
            return (false, null);
        }

        return (true, new() { { ResultKey, items } });
    }
}
