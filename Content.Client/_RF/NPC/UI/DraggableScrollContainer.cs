using System.Numerics;
using Robust.Client.Input;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;

namespace Content.Client._RF.NPC.UI;

public sealed class DraggableScrollContainer : ScrollContainer
{
    [Dependency] private readonly IInputManager _input = default!;

    private bool _dragging;
    private Vector2 _lastMousePosition;

    public DraggableScrollContainer()
    {
        IoCManager.InjectDependencies(this);

        OnKeyBindDown += args =>
        {
            if (args.Function != EngineKeyFunctions.UIClick || args.IsRepeat)
                return;

            _dragging = true;
            _lastMousePosition = _input.MouseScreenPosition.Position;
            args.Handle();
        };

        OnKeyBindUp += args =>
        {
            if (args.Function != EngineKeyFunctions.UIClick)
                return;

            _dragging = false;
            args.Handle();
        };
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);

        if (!_dragging)
            return;

        var mouse = _input.MouseScreenPosition.Position;
        var delta = mouse - _lastMousePosition;
        _lastMousePosition = mouse;

        // Dragging the view: move content opposite to mouse movement.
        SetScrollValue(GetScrollValue() - delta);
        args.Handle();
    }
}
