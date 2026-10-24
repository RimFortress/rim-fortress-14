using Content.Client._RF.UserInterface.Scope;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client._RF.UserInterface.Controls;

/// <summary>
/// A <see cref="UIWidget"/> with a <see cref="UiScope"/>: what the widget does while it is in the tree of the controls.
/// </summary>
public abstract class RfWidget : UIWidget
{
    private UiScope? _scope;

    /// <summary>
    /// What the widget does while it is in the tree: listens to events, updates itself at an interval. See
    /// <see cref="UiScope"/>.
    /// </summary>
    public UiScope Scope => _scope ??= new UiScope(IsInsideTree);

    /// <summary>
    /// The UI controller of the type: <c>Get&lt;OptionsUIController&gt;().ToggleWindow()</c>.
    /// </summary>
    protected T Get<T>() where T : UIController, new() => UserInterfaceManager.GetUIController<T>();

    protected override void EnteredTree()
    {
        base.EnteredTree();
        _scope?.Enter();
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();
        _scope?.Exit();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        _scope?.Update(args.DeltaSeconds, VisibleInTree);
    }
}
