using System.Linq;
using Content.Client._RF.Selection;
using Content.Client.ContextMenu.UI;
using Content.Shared._RF.NPC.Components;
using Content.Shared._RF.NPC.UtilityAi.Prototypes;
using Content.Shared._RF.NPC.UtilityAi.Systems;
using Content.Shared.Verbs;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._RF.NPC.UtilityAi.Systems;

public sealed class ExecutableGoalSystem : SharedExecutableGoalSystem
{
    [Dependency] private readonly IUserInterfaceManager _ui = default!;
    [Dependency] private readonly IOverlayManager _overlay = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly SelectionSystem _selection = default!;

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

        SubscribeLocalEvent<GetVerbsEvent<Verb>>(OnGetVerbs);
        SubscribeNetworkEvent<NpcGoalsContextMenuMessage>(OnContextMenu);

        _overlay.AddOverlay(new NpcControlOverlay());
        DefaultSelection();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlay.RemoveOverlay<NpcControlOverlay>();
    }

    private void OnGetVerbs(GetVerbsEvent<Verb> ev)
    {
        if (!TryComp(ev.User, out NpcControlComponent? control))
            return;

        var tasks = new Dictionary<ExecutableGoalPrototype, List<EntityUid>>();
        var prototypes = control.Goals.Select(Proto.Index).ToList();

        foreach (var entity in _selection.Selected)
        {
            if (FindSatisfiedGoals(entity, ev.Target, prototypes) is not { } suitable)
                continue;

            foreach (var task in suitable)
            {
                if (!tasks.TryAdd(task, new()))
                    tasks[task].Add(entity);
            }
        }

        foreach (var goal in control.Goals)
        {
            if (!Proto.Resolve(goal, out var proto)
                || proto.TaskType == ExecutableGoalPrototype.ExecutableGoalType.Place)
                continue;

            if (!Whitelist.IsWhitelistPassOrNull(proto.TargetWhitelist, ev.Target))
                continue;

            ev.Verbs.Add(new()
            {
                Text = Proto.Index(proto.Goal).Name,
                Icon = proto.VerbIcon,
                Category = VerbCategory.NpcTask,
                Act = () =>
                {
                    RaisePredictiveEvent(new SetGoalRequest
                    {
                        Goal = goal,
                        Entities = GetNetEntityList(_selection.Selected),
                        Target = GetNetEntity(ev.Target),
                    });
                },
            });
        }
    }

    private void OnContextMenu(NpcGoalsContextMenuMessage ev, EntitySessionEventArgs args)
    {
        OpenContextMenu(args.SenderSession, GetEntity(ev.Target));
    }

    protected override void OpenContextMenu(ICommonSession player, EntityUid uid)
    {
        _ui.GetUIController<EntityMenuUIController>().OpenRootMenu(new() { uid });
    }

    #region Selection

    public void SetSelectedTask(ProtoId<ExecutableGoalPrototype>? taskId)
    {
        if (taskId == SelectedTask
            || !ControllableQuery.HasComp(_player.LocalEntity))
            return;

        SelectedTask = taskId;
        Eraser = false;

        if (Proto.TryIndex(SelectedTask, out var proto)
            && Proto.Resolve(proto.Goal, out var goal))
        {
            _selection.SetSelection(
                act: _ => SetSelectedTask(null),
                onSelected: entities
                    => RaisePredictiveEvent(new PassiveGoalRequest(
                        proto,
                        entities.Select(x => GetNetEntity(x)).ToList())),
                filter: NpcTaskFilter,
                color: goal.Color,
                icon: proto.VerbIcon,
                iconColor: goal.Color);
        }
        else
            DefaultSelection();
    }

    public void SetEraser(bool enabled)
    {
        if (Eraser == enabled)
            return;

        if (!ControllableQuery.HasComp(_player.LocalEntity))
            return;

        Eraser = enabled;
        SelectedTask = null;

        if (Eraser)
        {
            _selection.SetSelection(
                act: _ => SetEraser(false),
                onSelected: entities
                    => RaisePredictiveEvent(new PassiveGoalRemoveRequest(
                        entities.Select(x => GetNetEntity(x)).ToList())),
                icon: EraseIcon);
        }
        else
            DefaultSelection();
    }

    private bool NpcTaskFilter(EntityUid uid) =>
        Proto.TryIndex(SelectedTask, out var proto) && Whitelist.IsWhitelistPassOrNull(proto.TargetWhitelist, uid);

    private bool NpcFilter(EntityUid uid) => GoapQuery.HasComp(uid);

    public void DefaultSelection()
    {
        _selection.SetSelection(
            act: args =>
            {
                if (!ControlQuery.HasComp(_player.LocalEntity)
                    || args.Selected.Count == 0)
                    return;

                RaiseNetworkEvent(new SetGoalRequest
                {
                    Entities = args.Selected.Select(x => GetNetEntity(x)).ToList(),
                    Target = GetNetEntity(args.ActUid),
                    TargetCoordinates = GetNetCoordinates(args.ActCoords),
                });
            },
            filter: NpcFilter);
    }

    #endregion
}
