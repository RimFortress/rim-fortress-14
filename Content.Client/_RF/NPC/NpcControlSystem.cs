using System.Linq;
using Content.Client.ContextMenu.UI;
using Content.Client.NPC.HTN;
using Content.Shared._RF.NPC;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.UserInterface;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Client._RF.NPC;

public sealed class NpcControlSystem : SharedNpcControlSystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IOverlayManager _overlay = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;

    /// <summary>
    /// Selection frame start point
    /// </summary>
    public MapCoordinates? StartPoint { get; private set; }

    /// <summary>
    /// Selection frame endpoint
    /// </summary>
    public MapCoordinates? EndPoint { get; private set; }

    /// <summary>
    /// Entities within the boundaries of the selection frame
    /// </summary>
    public HashSet<EntityUid> Selected { get; private set; } = new();

    /// <summary>
    /// Current tasks of entities that are known to the client
    /// </summary>
    public Dictionary<EntityUid, NpcTask> Tasks { get; } = new();

    // TODO: Instead, it is worth storing this in the client side of the PassiveTaskTargetComponent
    public Dictionary<NpcTask, List<EntityUid>> PassiveTasks { get; } = new();

    /// <summary>
    /// Client information about tasks available to the client entity
    /// </summary>
    public Dictionary<string, NpcTask> TasksData { get; private set; } = new();

    /// <summary>
    /// Name of the current task selected to issue passive tasks, if any
    /// </summary>
    public NpcTask? SelectedTask { get; private set; }

    public bool Eraser { get; private set; }

    public event Action? OnTaskData;
    public event Action? OnUpdateSelectMode;

    private EntityQuery<NpcControlComponent> _controlQuery;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        _overlay.AddOverlay(new NpcControlOverlay());

        CommandBinds.Builder
            .Bind(EngineKeyFunctions.Use, new PointerStateInputCmdHandler(OnSelectEnabled, OnSelectDisabled))
            .Bind(EngineKeyFunctions.UseSecondary, new PointerInputCmdHandler(OnUseSecondary))
            .Register<SharedNpcControlSystem>();

        SubscribeNetworkEvent<NpcTaskInfoMessage>(OnTaskInfo);
        SubscribeNetworkEvent<NpcTaskFinishMessage>(OnTaskFinished);
        SubscribeNetworkEvent<AllowedNpcTasksInfoMessage>(OnAllowedTasksInfo);
        SubscribeNetworkEvent<PassiveNpcTaskMessage>(OnPassiveTask);
        SubscribeNetworkEvent<PassiveNpcTaskRemoveMessage>(OnPassiveTaskRemove);
        SubscribeNetworkEvent<NpcTasksContextMenuMessage>(OnContextMenu);

        SubscribeLocalEvent<NpcControlComponent, PlayerAttachedEvent>(OnAttached);

        _controlQuery = GetEntityQuery<NpcControlComponent>();
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _overlay.RemoveOverlay<NpcControlOverlay>();
    }

    private bool OnSelectEnabled(ICommonSession? player, EntityCoordinates coords, EntityUid uid)
    {
        if (player?.AttachedEntity is not { Valid: true } entity
            || !_controlQuery.TryComp(entity, out _))
            return false;

        StartPoint = _transform.ToMapCoordinates(coords);
        EndPoint = StartPoint;
        return false;
    }

    private bool OnSelectDisabled(ICommonSession? player, EntityCoordinates coords, EntityUid uid)
    {
        if (player?.AttachedEntity is not { Valid: true } entity
            || !_controlQuery.TryComp(entity, out _))
            return false;

        if (Eraser && Selected.Count != 0)
        {
            var msg = new PassiveNpcTaskRemoveRequest(
                GetNetEntity(entity),
                Selected.Select(x => GetNetEntity(x)).ToList());

            RaiseNetworkEvent(msg);
            Selected.Clear();
        }

        if (SelectedTask != null && Selected.Count != 0)
        {
            var msg = new PassiveNpcTaskRequest(
                GetNetEntity(entity),
                SelectedTask.TaskId,
                Selected.Select(x => GetNetEntity(x)).ToList());

            RaiseNetworkEvent(msg);
            Selected.Clear();
        }

        StartPoint = null;
        EndPoint = null;
        return false;
    }

    private bool OnUseSecondary(ICommonSession? player, EntityCoordinates coords, EntityUid uid)
    {
        if (SelectedTask != null)
            SetSelectedTask(null);

        if (Eraser)
            SetEraser(false);

        if (player is not { AttachedEntity: { Valid: true} requester }
            || SelectedTask != null
            || Eraser
            || Selected.Count == 0)
            return false;

        RaiseNetworkEvent(new NpcTaskRequest
        {
            Requester = GetNetEntity(requester),
            Entities = Selected.Select(entity => GetNetEntity(entity)).ToList(),
            Target = uid.IsValid() ? GetNetEntity(uid) : null,
            TargetCoordinates = GetNetCoordinates(coords),
        });

        return true;
    }

    private void OnTaskInfo(NpcTaskInfoMessage msg)
    {
        Tasks[GetEntity(msg.Entity)] = new NpcTask(msg, EntityManager);
    }

    private void OnTaskFinished(NpcTaskFinishMessage msg)
    {
        var uid = GetEntity(msg.Entity);
        Tasks.Remove(uid);

        if (TasksData.TryGetValue(msg.TaskId, out var task)
            && PassiveTasks.TryGetValue(task, out var targets))
            targets.Remove(uid);
    }

    private void OnAllowedTasksInfo(AllowedNpcTasksInfoMessage msg)
    {
        TasksData = msg.Info.Select(x => (x.TaskId, new NpcTask(x, EntityManager))).ToDictionary();
        OnTaskData?.Invoke();
    }

    private void OnPassiveTask(PassiveNpcTaskMessage msg)
    {
        if (!TasksData.TryGetValue(msg.TaskId, out var npcTask))
            return;

        if (PassiveTasks.TryGetValue(npcTask, out var targets))
            targets.AddRange(msg.Entities.Select(GetEntity));
        else
            PassiveTasks[npcTask] = msg.Entities.Select(GetEntity).ToList();
    }

    private void OnPassiveTaskRemove(PassiveNpcTaskRemoveMessage msg)
    {
        var entities = msg.Entities.Select(GetEntity).ToList();

        foreach (var (_, targets) in PassiveTasks)
        {
            foreach (var uid in entities)
            {
                targets.Remove(uid);
            }
        }
    }

    private void OnContextMenu(NpcTasksContextMenuMessage msg)
    {
        _ui.GetUIController<EntityMenuUIController>().OpenRootMenu(new() { GetEntity(msg.Target) });
    }

    private void OnAttached(EntityUid uid, NpcControlComponent component, PlayerAttachedEvent args)
    {
        var msg = new AllowedNpcTasksInfoRequest(GetNetEntity(uid));
        RaiseNetworkEvent(msg);
    }

    public void SetSelectedTask(string? taskId)
    {
        if (taskId == SelectedTask?.TaskId || taskId != null && !TasksData.ContainsKey(taskId))
            return;

        SelectedTask = taskId != null ? TasksData[taskId] : null;
        Eraser = false;
        Selected.Clear();
        OnUpdateSelectMode?.Invoke();
    }

    public void SetEraser(bool enabled)
    {
        if (Eraser == enabled)
            return;

        Eraser = enabled;
        SelectedTask = null;
        Selected.Clear();
        OnUpdateSelectMode?.Invoke();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (StartPoint is not { } start
            || EndPoint is not { } end
            || start.MapId != end.MapId)
        {
            StartPoint = null;
            EndPoint = null;
            return;
        }

        if (_input.MouseScreenPosition is { IsValid: true } mousePos)
            EndPoint = _eye.PixelToMap(mousePos);

        Selected = SelectedTask == null && !Eraser
            ? GetNpcInSelect()
            : _lookup.GetEntitiesIntersecting(start.MapId, new Box2(start.Position, end.Position));
    }

    /// <summary>
    /// Gets the list of NPCs in the selection area
    /// </summary>
    private HashSet<EntityUid> GetNpcInSelect()
    {
        if (StartPoint is not { } start
            || EndPoint is not { } end
            || start.MapId != end.MapId)
            return new HashSet<EntityUid>();

        var area = new Box2(start.Position, end.Position);
        var entities = _lookup.GetEntitiesIntersecting(start.MapId, area, LookupFlags.Dynamic);

        foreach (var entity in entities)
        {
            if (!TryComp(entity, out HTNComponent? _))
                entities.Remove(entity);
        }

        return entities;
    }
}
