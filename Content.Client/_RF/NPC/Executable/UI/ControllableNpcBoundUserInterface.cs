using System.Linq;
using Content.Client._RF.NPC.Executable.Systems;
using Content.Client._RF.Selection;
using Content.Client.UserInterface.Controls;
using Content.Shared._RF.NPC.Executable.Components;
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
public sealed class ControllableNpcBoundUserInterface : BoundUserInterface
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IInputManager _input = default!;

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

        if (exec.UiTarget == null || !EntMan.TryGetComponent(Owner, out NpcControllerComponent? comp))
            return;

        var buttons = GetButtons(exec.UiTarget.Value, comp);

        if (buttons.Count == 0)
        {
            Close();
            return;
        }

        _menu!.Track(exec.UiTarget.Value);
        _menu.SetButtons(buttons);
    }

    private Dictionary<ExecutableGoalPrototype, List<EntityUid>> GetTasks(EntityUid target, NpcControllerComponent comp)
    {
        var tasks = new Dictionary<ExecutableGoalPrototype, List<EntityUid>>();
        var prototypes = comp.Goals.Select(_proto.Index).ToList();
        var selection = EntMan.System<SelectionSystem>();
        var exec = EntMan.System<ExecutableGoalSystem>();

        foreach (var entity in selection.SelectedEntities(Owner))
        {
            if (!exec.CanControl(Owner, entity)
                || exec.FindSatisfiedGoals(entity, target, prototypes, ExecutableGoalType.Verb) is not { } suitable)
                continue;

            foreach (var task in suitable)
            {
                if (!tasks.TryAdd(task, new()))
                    tasks[task].Add(entity);
                else
                    tasks[task] = new() { entity };
            }
        }

        return tasks;
    }

    private ValueList<RadialMenuOptionBase> GetButtons(EntityUid target, NpcControllerComponent comp)
    {
        var tasks = GetTasks(target, comp);
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

