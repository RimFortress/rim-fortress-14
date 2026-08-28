using System.Linq;
using Content.Client._RF.Selection;
using Content.Client.Verbs.UI;
using Content.Shared._RF.NPC.Executable.Components;
using Content.Shared._RF.NPC.Executable.Prototypes;
using Content.Shared._RF.NPC.Executable.Systems;
using Content.Shared.CombatMode;
using Content.Shared.Input;
using Robust.Client.GameStates;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._RF.NPC.Executable.Systems;

public sealed class ExecutableGoalSystem : SharedExecutableGoalSystem
{
    [Dependency] private readonly IUserInterfaceManager _ui = default!;
    [Dependency] private readonly IOverlayManager _overlay = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly SelectionSystem _selection = default!;
    [Dependency] private readonly IClientNetManager _net = default!;
    [Dependency] private readonly IClientGameStateManager _state = default!;

    private static readonly SpriteSpecifier EraseIcon
        = new SpriteSpecifier.Texture(new("/Textures/_RF/Interface/VerbIcons/eraser-solid.svg.192dpi.png"));

    /// <summary>
    /// Name of the current goal selected to issue passive tasks, if any
    /// </summary>
    public ProtoId<ExecutableGoalPrototype>? SelectedTask { get; private set; }

    public bool Eraser { get; private set; }

    public override void Initialize()
    {
        base.Initialize();

        _overlay.AddOverlay(new NpcControlOverlay());

        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);

        CommandBinds.Builder
            .Bind(ContentKeyFunctions.NpcCombatModeToggle, new PointerInputCmdHandler(OnCombatToggle))
            .Register<ExecutableGoalSystem>();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlay.RemoveOverlay<NpcControlOverlay>();
    }

    private void OnPlayerAttached(PlayerAttachedEvent args)
    {
        DefaultSelection();
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
                icon: proto.VerbIcon,
                iconColor: goal.Color,
                netSync: true);
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
                icon: EraseIcon,
                netSync: true);
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
                    _ui.GetUIController<VerbMenuUIController>().OpenVerbMenu(uid);
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
            filter: NpcFilter,
            netSync: true);
    }

    #endregion
}
