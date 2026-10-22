using Content.Client._RF.UserInterface.Scope;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Timing;

namespace Content.Client._RF.Suil;

public partial class UiElement
{
    // Made on the first use: a control that never asks for the scope pays for nothing.
    private UiScope? _scope;

    /// <summary>
    /// What the control does while it is in the tree: listens to events, updates itself at an interval. See
    /// <see cref="UiScope"/>.
    /// </summary>
    public UiScope Scope => _scope ??= new UiScope(IsInsideTree);

    [ProxyFor(typeof(IUserInterfaceManager), nameof(IUserInterfaceManager.GetUIController))]
    protected T Controller<T>() where T : UIController, new() => UserInterfaceManager.GetUIController<T>();

    protected static T System<T>() where T : EntitySystem, new() => IoCManager.Resolve<IEntityManager>().System<T>();

    /// <inheritdoc/>
    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        _scope?.Update(args.DeltaSeconds, VisibleInTree);
    }
}
