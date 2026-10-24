using Content.Client._RF.Construction;
using Content.Client._RF.NPC.Executable.Systems;
using Content.Client._RF.Selection;
using Content.Client._RF.UserInterface.Controllers;
using Content.Client._RF.UserInterface.Scope;
using Content.Client._RF.World;
using Content.Client._RF.Zoning.UI;
using Content.Shared._RF.NPC.Executable.Components;
using Content.Shared._RF.NPC.Executable.Prototypes;
using Content.Shared._RF.Zoning.Components;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Content.Client._RF.NpcJobs.UI;
using Content.Client._RF.UserInterface.Controls;

namespace Content.Client._RF.ActionsHotbar.UI;

public sealed partial class ActionsHotbarWidget : RfWidget
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IEntityManager _entity = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    private readonly ZoningUiController _zoningController;

    private ExecutableGoalSystem _executable = default!;
    private SpriteSystem _sprite = default!;

    private readonly Dictionary<ProtoId<ExecutableGoalPrototype>, TreeMenuButton> _taskButtons = new();
    private TreeMenuButton? _taskEraseButton;
    private bool _resolved;

    public ActionsHotbarWidget()
    {
        InitializeComponent();

        _zoningController = Get<ZoningUiController>();

        BuildButton.OnPressed += _ => Get<ConstructionMenuUiController>().ToggleWindow();
        NpcJobsButton.OnPressed += _ => Get<NpcJobsUiController>().TogglePriorityWindow();
        WorldMapButton.OnPressed += _ => Get<WorldMapUiController>().ToggleWindow();

        Scope.ListenSystem<SelectionSystem>(
            (s, h) => s.OnUpdateSelection += h,
            (s, h) => s.OnUpdateSelection -= h,
            UpdateButtons);
        Scope.ListenSystem<ExecutableGoalSystem>(
            (s, h) => s.OnControllerAttached += h,
            (s, h) => s.OnControllerAttached -= h,
            UpdateList);
    }

    private void ResolveSystems()
    {
        if (_resolved)
            return;

        _resolved = true;
        _executable = _entity.System<ExecutableGoalSystem>();
        _sprite = _entity.System<SpriteSystem>();
    }

    public void EnsureSetup()
    {
        UpdateList();
    }

    private void UpdateList()
    {
        ResolveSystems();

        PassiveTasksButton.ClearContent();
        _taskButtons.Clear();

        if (_taskEraseButton == null)
        {
            _taskEraseButton = new TreeMenuButton
            {
                TexturePath = "/Textures/_RF/Interface/VerbIcons/eraser-solid.svg.192dpi.png",
                ToolTip = Loc.GetString("passive-tasks-menu-eraser-tooltip"),
                ToggleMode = true,
            };

            _taskEraseButton.OnToggled += args => _executable.SetEraser(args.Pressed);
            PassiveTasksButton.AddContent(_taskEraseButton);
        }

        if (_entity.TryGetComponent(_player.LocalEntity, out NpcControllerComponent? control))
        {
            foreach (var goal in control.Goals)
            {
                if (!_proto.Resolve(goal, out var proto)
                    || !proto.GoalType.HasFlag(ExecutableGoalType.Passive))
                    continue;

                var button = new TreeMenuButton
                {
                    ToolTip = Loc.GetString(_proto.Index(proto.Goal).Name),
                    ToggleMode = true,
                    Texture = proto.Icon != null ? _sprite.Frame0(proto.Icon) : null,
                };

                button.OnToggled += args =>
                    _executable.SetSelectedTask(args.Pressed ? (ProtoId<ExecutableGoalPrototype>?)goal : null);
                _taskButtons.Add(goal, button);
                PassiveTasksButton.AddContent(button);
            }
        }

        ZoningButton.ClearContent();

        if (_entity.TryGetComponent(_player.LocalEntity, out ZoneCreatorComponent? zoneCreator))
        {
            var editButton = new TreeMenuButton
            {
                ToolTip = Loc.GetString("actions-hotbar-widgets-create-edit-tooltip"),
                TexturePath = "/Textures/_RF/Interface/pencil-solid.svg.192dpi.png",
            };
            editButton.OnPressed += _ => _zoningController.PickZone();
            ZoningButton.AddContent(editButton);

            foreach (var zone in zoneCreator.Zones)
            {
                if (!_proto.Resolve(zone, out var proto)
                    || !proto.TryComp(out ZoneVisualsComponent? comp, _entity.ComponentFactory))
                    continue;

                var button = new TreeMenuButton
                {
                    ToolTip = proto.Name,
                    Texture = comp.Icon != null ? _sprite.Frame0(comp.Icon) : null,
                };
                ZoningButton.AddContent(button);
                button.OnPressed += _ => _zoningController.CreateSelection(zone);
            }
        }
    }

    private void UpdateButtons()
    {
        ResolveSystems();

        foreach (var (task, button) in _taskButtons)
        {
            button.Pressed = task == _executable.SelectedTask;
        }

        if (_taskEraseButton != null)
            _taskEraseButton.Pressed = _executable.Eraser;
    }
}

