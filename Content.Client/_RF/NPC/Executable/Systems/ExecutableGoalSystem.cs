using System.Linq;
using Content.Client._RF.NPC.Executable.UI;
using Content.Client._RF.Selection;
using Content.Shared._RF.NPC.Executable.Components;
using Content.Shared._RF.NPC.Executable.Prototypes;
using Content.Shared._RF.NPC.Executable.Systems;
using Content.Shared.CombatMode;
using Content.Shared.Input;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._RF.NPC.Executable.Systems;

public sealed partial class ExecutableGoalSystem : SharedExecutableGoalSystem
{
    [Dependency] private IOverlayManager _overlay = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IInputManager _input = default!;
    [Dependency] private SelectionSystem _selection = default!;
    [Dependency] private UserInterfaceSystem _uiSystem = default!;

    private static readonly SpriteSpecifier EraseIcon
        = new SpriteSpecifier.Texture(new("/Textures/_RF/Interface/VerbIcons/eraser-solid.svg.192dpi.png"));

    /// <summary>
    /// Name of the current goal selected to issue passive tasks, if any
    /// </summary>
    public ProtoId<ExecutableGoalPrototype>? SelectedTask { get; private set; }

    public bool Eraser { get; private set; }

    [Access(typeof(ControllableNpcBoundUserInterface))]
    public EntityUid? UiTarget;

    [Access(typeof(ControllableNpcBoundUserInterface))]
    public IReadOnlyDictionary<ExecutableGoalPrototype, List<EntityUid>>? UiTasks;

    public event Action? OnControllerAttached;

    public override void Initialize()
    {
        base.Initialize();

        _overlay.AddOverlay(new NpcControlOverlay());

        CommandBinds.Builder
            .Bind(ContentKeyFunctions.NpcCombatModeToggle, new PointerInputCmdHandler(OnCombatToggle))
            .Register<ExecutableGoalSystem>();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlay.RemoveOverlay<NpcControlOverlay>();
    }

    [SubscribeLocalEvent]
    private void OnPlayerAttached(Entity<NpcControllerComponent> ent, ref PlayerAttachedEvent args)
    {
        DefaultSelection();
        OnControllerAttached?.Invoke();
    }

    private bool OnCombatToggle(ICommonSession? player, EntityCoordinates coords, EntityUid uid)
    {
        if (!ControllerQuery.HasComp(player?.AttachedEntity))
            return false;

        var selected = _selection
            .SelectedEntities()
            .Where(HasComp<CombatModeComponent>)
            .Select(x => new Entity<CombatModeComponent>(x, Comp<CombatModeComponent>(x)))
            .ToList();

        if (selected.Count == 0)
            return false;

        var mode = selected.Count(x => x.Comp.IsInCombatMode) > selected.Count / 2;
        SetCombatMode(!mode);
        return true;
    }

    protected override bool NeedForceGoalExecution()
        => !_input.DownKeyFunctions.Contains(ContentKeyFunctions.NpcGoalAddToQueue);

    public void SetCombatMode(bool combatMode)
    {
        if (!ControllerQuery.TryComp(_player.LocalEntity, out var comp))
            return;

        var selected = _selection
            .SelectedEntities()
            .Where(HasComp<CombatModeComponent>)
            .ToList();

        if (selected.Count == 0)
            selected = comp.CanControl.ToList();

        SetCombatMode(_player.LocalEntity.Value, selected, combatMode);
    }

    private Dictionary<ExecutableGoalPrototype, List<EntityUid>> GetTasks(EntityUid ent)
    {
        var tasks = new Dictionary<ExecutableGoalPrototype, List<EntityUid>>();

        if (!TryComp(_player.LocalEntity, out NpcControllerComponent? comp))
            return tasks;

        var prototypes = comp.Goals.Select(Proto.Index).ToList();

        foreach (var uid in _selection.SelectedEntities())
        {
            if (!CanControl(_player.LocalEntity.Value, uid)
                || FindSatisfiedGoals(uid, ent, prototypes, ExecutableGoalType.Verb) is not { } suitable)
                continue;

            foreach (var task in suitable)
            {
                if (!tasks.TryAdd(task, new()))
                    tasks[task].Add(uid);
                else
                    tasks[task] = new() { uid };
            }
        }

        return tasks;
    }

    #region Selection

    public void SetSelectedTask(ProtoId<ExecutableGoalPrototype>? taskId)
    {
        if (taskId == SelectedTask
            || !ControllerQuery.HasComp(_player.LocalEntity))
            return;

        SelectedTask = taskId;
        Eraser = false;

        if (Proto.TryIndex(SelectedTask, out var proto)
            && Proto.Resolve(proto.Goal, out var goal))
        {
            _selection.SetSelection(
                act: _ => SetSelectedTask(null),
                onSelected: entities =>
                {
                    if (Timing.IsFirstTimePredicted)
                    {
                        RaisePredictiveEvent(new PassiveGoalRequest(
                            proto,
                            entities.Select(x => GetNetEntity(x)).ToList()));
                    }
                },
                filter: NpcTaskFilter,
                color: goal.Color,
                icon: proto.Icon,
                iconColor: goal.Color);
        }
        else
            DefaultSelection();
    }

    public void SetEraser(bool enabled)
    {
        if (Eraser == enabled)
            return;

        if (!ControllerQuery.HasComp(_player.LocalEntity))
            return;

        Eraser = enabled;
        SelectedTask = null;

        if (Eraser)
        {
            _selection.SetSelection(
                act: _ => SetEraser(false),
                onSelected: entities =>
                {
                    if (!Timing.IsFirstTimePredicted)
                        return;

                    RaisePredictiveEvent(new PassiveGoalRemoveRequest(
                        entities.Select(x => GetNetEntity(x)).ToList()));
                    _selection.ClearSelection();
                },
                icon: EraseIcon);
        }
        else
            DefaultSelection();
    }

    private bool NpcTaskFilter(EntityUid uid) =>
        Proto.TryIndex(SelectedTask, out var proto)
        && Whitelist.IsWhitelistPassOrNull(proto.TargetWhitelist, uid);

    private bool NpcFilter(EntityUid uid) => GoapQuery.HasComp(uid);

    public void DefaultSelection()
    {
        _selection.SetSelection(
            act: args =>
            {
                if (!ControllerQuery.HasComp(_player.LocalEntity)
                    || args.Selected.Count == 0)
                    return;

                if (args.ActUid is { } uid)
                {
                    if (_player.LocalEntity is not { } playerUid)
                        return;

                    var tasks = GetTasks(uid);

                    if (tasks.Count == 0
                        || !_uiSystem.TryOpenUi(playerUid, NpcControllerUiKey.Key, playerUid, true))
                        return;

                    UiTarget = uid;
                    UiTasks = tasks;
                    return;
                }

                if (!Timing.IsFirstTimePredicted)
                    return;

                RaisePredictiveEvent(new SetGoalRequest
                {
                    Entities = args.Selected.Select(x => GetNetEntity(x)).ToList(),
                    TargetCoordinates = GetNetCoordinates(args.ActCoords),
                    AddToQueue = !NeedForceGoalExecution(),
                });
            },
            filter: NpcFilter);
    }

    #endregion
}
