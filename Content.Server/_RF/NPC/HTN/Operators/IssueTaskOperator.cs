using System.Threading;
using System.Threading.Tasks;
using Content.Server._RF.NPC.Prototypes;
using Content.Server._RF.NPC.Systems;
using Content.Server.NPC;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.HTN.Operators;

/// <summary>
/// Forces another NPC to complete a task.
/// </summary>
public sealed partial class IssueTaskOperator : HTNOperator
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IEntityManager _entity = default!;

    private NpcControlSystem _control;

    /// <summary>
    /// Task to complete
    /// </summary>
    [DataField(required: true)]
    public ProtoId<NpcTaskPrototype> Task;

    /// <summary>
    /// The key that stores the entity to issue the task to
    /// </summary>
    [DataField]
    public string TargetKey = "TargetKey";

    /// <summary>
    /// The key that stores the list of entities that need to be assigned a task.
    /// Higher priority than TargetKey
    /// </summary>
    [DataField]
    public string? TargetsKey;

    /// <summary>
    /// The key whose value will be passed as the target entity of the task
    /// </summary>
    [DataField]
    public string? TaskTarget;

    /// <summary>
    /// The key whose value will be passed as the target coordinates of the task
    /// </summary>
    [DataField]
    public string? TaskCoordinates;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _control = sysManager.GetEntitySystem<NpcControlSystem>();
    }

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard, CancellationToken cancelToken)
    {
        if (!_prototype.TryIndex(Task, out var proto))
            return (false, null);

        var uids = new List<EntityUid>();

        if (TargetsKey != null)
        {
            if (!blackboard.TryGetValue(TargetsKey, out List<EntityUid>? targets, _entity))
                return (false, null);

            uids = targets;
        }
        else if (blackboard.TryGetValue(TargetKey, out EntityUid? uid, _entity))
            uids.Add(uid.Value);

        if (uids.Count == 0)
            return (false, null);

        if (TaskTarget != null)
        {
            if (!blackboard.TryGetValue(TaskTarget, out EntityUid? taskTarget, _entity))
                return (false, null);

            foreach (var uid in uids)
            {
                if (!_control.TrySetTask(uid, proto, taskTarget.Value))
                    return (false, null);
            }
        }
        else if (TaskCoordinates != null)
        {
            if (!blackboard.TryGetValue(TaskCoordinates, out EntityCoordinates? taskTarget, _entity))
                return (false, null);

            foreach (var uid in uids)
            {
                if (!_control.TrySetTask(uid, proto, taskTarget.Value))
                    return (false, null);
            }
        }
        else
        {
            foreach (var uid in uids)
            {
                if (!_control.TrySetTask(uid, proto))
                    return (false, null);
            }
        }

        return (true, null);
    }
}
