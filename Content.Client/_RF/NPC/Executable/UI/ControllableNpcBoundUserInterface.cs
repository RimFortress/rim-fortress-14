using System.Linq;
using Content.Client._RF.NPC.Executable.Systems;
using Content.Client.UserInterface.Controls;
using Content.Shared._RF.NPC.Executable.Prototypes;
using Content.Shared._RF.NPC.Executable.Systems;
using Content.Shared.Input;
using JetBrains.Annotations;
using Robust.Client.Input;
using Robust.Client.UserInterface;
using Robust.Shared.Collections;
using Robust.Shared.Prototypes;

namespace Content.Client._RF.NPC.Executable.UI;

[UsedImplicitly]
public sealed partial class ControllableNpcBoundUserInterface : BoundUserInterface
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IInputManager _input = default!;

    private SimpleRadialMenu? _menu;

    public ControllableNpcBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Open()
    {
        base.Open();
        _menu = this.CreateWindow<SimpleRadialMenu>();
        var exec = EntMan.System<ExecutableGoalSystem>();

        if (exec.UiTarget == null || exec.UiTasks == null)
            return;

        var buttons = GetButtons(exec.UiTarget.Value, exec.UiTasks);
        _menu.Track(exec.UiTarget.Value);
        _menu.SetButtons(buttons);
    }

    private ValueList<RadialMenuOptionBase> GetButtons(
        EntityUid target,
        IReadOnlyDictionary<ExecutableGoalPrototype, List<EntityUid>> tasks)
    {
        ValueList<RadialMenuOptionBase> buttons = new();
        buttons.Capacity = tasks.Count;

        foreach (var (prototype, entities) in tasks)
        {
            if (!_proto.Resolve(prototype.Goal, out var goal))
                continue;

            var action = new RadialMenuActionOption<ExecutableGoalPrototype>(Handle, prototype)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(prototype.Icon),
                ToolTip = Loc.GetString(goal.Name),
            };
            buttons.Add(action);
            continue;

            void Handle(ExecutableGoalPrototype proto)
            {
                EntMan.RaisePredictiveEvent(new SetVerbGoalRequest(
                    EntMan.GetNetEntityList(entities),
                    proto,
                    EntMan.GetNetEntity(target),
                    !_input.DownKeyFunctions.Contains(ContentKeyFunctions.NpcGoalAddToQueue)));
            }
        }

        return buttons;
    }
}

