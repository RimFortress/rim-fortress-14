using System.Linq;
using Content.Client._RF.Selection;
using Content.Client.ContextMenu.UI;
using Content.Client.NPC.HTN;
using Content.Shared._RF.NPC;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared.Player;

namespace Content.Client._RF.NPC;

public sealed class NpcControlSystem : SharedNpcControlSystem
{
    [Dependency] private readonly IOverlayManager _overlay = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;
    [Dependency] private readonly SelectionSystem _selection = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    private const string EraseIcon = "/Textures/_RF/Interface/VerbIcons/eraser-solid.svg.192dpi.png";

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

    /// <summary>
    /// Called when the current entity task is changed
    /// </summary>
    public event Action<EntityUid>? OnTaskUpdated;

    private EntityQuery<NpcControlComponent> _controlQuery;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        _overlay.AddOverlay(new NpcControlOverlay());

        SubscribeNetworkEvent<NpcTaskInfoMessage>(OnTaskInfo);
        SubscribeNetworkEvent<NpcTaskFinishMessage>(OnTaskFinished);
        SubscribeNetworkEvent<AllowedNpcTasksInfoMessage>(OnAllowedTasksInfo);
        SubscribeNetworkEvent<PassiveNpcTaskMessage>(OnPassiveTask);
        SubscribeNetworkEvent<PassiveNpcTaskRemoveMessage>(OnPassiveTaskRemove);
        SubscribeNetworkEvent<NpcTasksContextMenuMessage>(OnContextMenu);

        SubscribeLocalEvent<NpcControlComponent, PlayerAttachedEvent>(OnAttached);

        _controlQuery = GetEntityQuery<NpcControlComponent>();

        DefaultSelection();
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _overlay.RemoveOverlay<NpcControlOverlay>();
    }

    private void OnTaskInfo(NpcTaskInfoMessage msg)
    {
        var uid = GetEntity(msg.Entity);

        Tasks[uid] = new NpcTask(msg, EntityManager);
        OnTaskUpdated?.Invoke(uid);
    }

    private void OnTaskFinished(NpcTaskFinishMessage msg)
    {
        var uid = GetEntity(msg.Entity);
        Tasks.Remove(uid);

        if (TasksData.TryGetValue(msg.TaskId, out var task)
            && PassiveTasks.TryGetValue(task, out var targets))
            targets.Remove(uid);

        OnTaskUpdated?.Invoke(uid);
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
        RaiseNetworkEvent(new AllowedNpcTasksInfoRequest());
        RaiseNetworkEvent(new NpcJobsInfoRequest());
    }

    public void SetSelectedTask(string? taskId)
    {
        if (taskId == SelectedTask?.TaskId || taskId != null && !TasksData.ContainsKey(taskId))
            return;

        if (_player.LocalEntity is not { Valid: true } entity || !_controlQuery.TryComp(entity, out _))
            return;

        SelectedTask = taskId != null ? TasksData[taskId] : null;
        Eraser = false;

        if (SelectedTask != null)
        {
            _selection.SetSelection(
                act: _ => SetSelectedTask(null),
                onSelected: entities
                    => RaiseNetworkEvent(new PassiveNpcTaskRequest(
                        SelectedTask.TaskId,
                        entities.Select(x => GetNetEntity(x)).ToList())),
                color: SelectedTask?.Color,
                iconPath: SelectedTask?.IconPath,
                iconColor: SelectedTask?.Color);
        }
        else
            DefaultSelection();
    }

    public void SetEraser(bool enabled)
    {
        if (Eraser == enabled)
            return;

        if (_player.LocalEntity is not { Valid: true } entity || !_controlQuery.TryComp(entity, out _))
            return;

        Eraser = enabled;
        SelectedTask = null;

        if (Eraser)
        {
            _selection.SetSelection(
                act: _ => SetEraser(false),
                onSelected: entities
                    => RaiseNetworkEvent(new PassiveNpcTaskRemoveRequest(
                        entities.Select(x => GetNetEntity(x)).ToList())),
                iconPath: EraseIcon);
        }
        else
            DefaultSelection();
    }

    private bool NpcFilter(EntityUid uid)
    {
        return TryComp(uid, out HTNComponent? _);
    }

    public void DefaultSelection()
    {
        _selection.SetSelection(
            act: args =>
            {
                if (_player.LocalEntity is not { Valid: true } entity
                    || !_controlQuery.TryComp(entity, out _)
                    || args.Selected.Count == 0)
                    return;

                RaiseNetworkEvent(new NpcTaskRequest
                {
                    Entities = args.Selected.Select(x => GetNetEntity(x)).ToList(),
                    Target = GetNetEntity(args.ActUid),
                    TargetCoordinates = GetNetCoordinates(args.ActCoords),
                });
            },
            filter: NpcFilter);
    }
}
