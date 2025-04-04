using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Shared._RF.NPC;

namespace Content.Server._RF.NPC;

public sealed class NpcControlSystem : SharedNpcControlSystem
{
    [Dependency] private readonly HTNSystem _htn = default!;
    [Dependency] private readonly NPCSystem _npc = default!;

    private const string TargetKey = "Target";
    private const string TargetCoordinatesKey = "TargetCoordinates";

    private readonly Dictionary<EntityUid, HTNCompoundTask> _originCompounds = new();
    private readonly Dictionary<EntityUid, NpcTask> _tasks = new();

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<NpcTaskRequest>(OnTaskRequest);
        SubscribeNetworkEvent<NpcTaskResetRequest>(OnTaskResetRequest);
    }

    /// <summary>
    /// Creates a new task for the NPC and saves the old one
    /// </summary>
    private void SetTask(EntityUid entity, NpcTask task, string compoundTask)
    {
        if (!_npc.TryGetNpc(entity, out var npc)
            || npc is not HTNComponent htn)
            return;

        if (htn.Plan != null)
            _htn.ShutdownPlan(htn);

        if (!_originCompounds.ContainsKey(entity))
            _originCompounds[entity] = htn.RootTask;

        _tasks[entity] = task;
        htn.RootTask = new HTNCompoundTask { Task = compoundTask };
        _htn.Replan(htn);

        var msg = new NpcTaskInfoMessage
        {
            Entity = GetNetEntity(entity),
            TaskType = task.Type,
            Target = GetNetEntity(task.Target),
            TargetCoordinates = GetNetCoordinates(task.Coordinates),
        };

        RaiseNetworkEvent(msg);
    }

    private void OnTaskRequest(NpcTaskRequest request)
    {
        var entity = GetEntity(request.Entity);
        var requester = GetEntity(request.Requester);
        var target = GetEntity(request.Target);
        var targetCoords = GetCoordinates(request.TargetCoordinates);
        var task = new NpcTask(request.TaskType, target, targetCoords);

        if (!_npc.TryGetNpc(entity, out var npc)
            || !TryComp(entity, out ControllableNpcComponent? controllable)
            || !controllable.CanControl.Contains(requester)
            || !controllable.Compounds.TryGetValue(task.Type, out var compoundTask))
            return;

        npc.Blackboard.SetValue(TargetCoordinatesKey, targetCoords);
        npc.Blackboard.SetValue(TargetKey, target);
        SetTask(entity, task, compoundTask);
    }

    private void OnTaskResetRequest(NpcTaskResetRequest request)
    {
        var entity = GetEntity(request.Entity);
        var requester = GetEntity(request.Requester);

        if (!TryComp(entity, out ControllableNpcComponent? controllable)
            || !controllable.CanControl.Contains(requester)
            || !_tasks.ContainsKey(entity)
            || !_npc.TryGetNpc(entity, out var npc)
            || npc is not HTNComponent htn)
            return;

        if (htn.Plan != null)
            _htn.ShutdownPlan(htn);

        htn.RootTask = _originCompounds[entity];

        _originCompounds.Remove(entity);
        _tasks.Remove(entity);
    }
}
