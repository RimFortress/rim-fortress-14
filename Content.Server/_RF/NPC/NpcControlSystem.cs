using Content.Server._RF.World;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Shared._RF.NPC;

namespace Content.Server._RF.NPC;

public sealed class NpcControlSystem : SharedNpcControlSystem
{
    [Dependency] private readonly HTNSystem _htn = default!;
    [Dependency] private readonly NPCSystem _npc = default!;
    [Dependency] private readonly RimFortressWorldSystem _world = default!;

    [ValidatePrototypeId<HTNCompoundPrototype>]
    private const string MoveToCompound = "MoveToCompound";
    private const string MoveToTargetKey = "MoveToTargetCoordinates";
    private const string MoveToRangeKey = "MoveToCloseRange";

    [ValidatePrototypeId<HTNCompoundPrototype>]
    private const string AttackCompound = "AttackTargetCompound";
    private const string AttackTargetKey = "AttackTarget";
    private const string AttackTargetCoordinatesKey = "AttackTargetCoordinates";

    private const float MoveToCloseRange = 0.20f;

    private readonly Dictionary<EntityUid, HTNCompoundTask> _originCompounds = new();
    private readonly Dictionary<EntityUid, NpcTask> _tasks = new();

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<NpcMoveToRequest>(OnMoveToRequest);
        SubscribeNetworkEvent<NpcAttackRequest>(OnAttackRequest);
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
            MoveTo = GetNetCoordinates(task.MoveTo),
            Attack = GetNetEntity(task.Attack),
        };

        RaiseNetworkEvent(msg);
    }

    private void OnMoveToRequest(NpcMoveToRequest request)
    {
        var entity = GetEntity(request.Entity);
        var requester = GetEntity(request.Requester);

        if (!_npc.TryGetNpc(entity, out var npc)
            || !_world.IsPlayerFactionMember(requester, entity))
            return;

        var coordinates = GetCoordinates(request.Target);
        npc.Blackboard.SetValue(MoveToTargetKey, coordinates);
        npc.Blackboard.SetValue(MoveToRangeKey, MoveToCloseRange);

        SetTask(entity, new NpcTask(coordinates), MoveToCompound);
    }

    private void OnAttackRequest(NpcAttackRequest request)
    {
        var entity = GetEntity(request.Entity);
        var requester = GetEntity(request.Requester);
        var attackTarget = GetEntity(request.Attack);

        if (!_npc.TryGetNpc(entity, out var npc)
            || !_world.IsPlayerFactionMember(requester, entity)
            || _world.IsPlayerFactionMember(requester, attackTarget))
            return;

        var coordinates = Transform(attackTarget).Coordinates;
        npc.Blackboard.SetValue(AttackTargetCoordinatesKey, coordinates);
        npc.Blackboard.SetValue(AttackTargetKey, attackTarget);

        SetTask(entity, new NpcTask(attackTarget), AttackCompound);
    }

    private void OnTaskResetRequest(NpcTaskResetRequest request)
    {
        var entity = GetEntity(request.Entity);
        var requester = GetEntity(request.Requester);

        if (!_world.IsPlayerFactionMember(requester, entity)
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
