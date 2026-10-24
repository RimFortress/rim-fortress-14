using Robust.Client.Input;
using Robust.Client.UserInterface;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Client._RF.Suil.HotReload;

/// <summary>
/// Waits for a click on a control of the UI and tells which one it was. The control under the mouse is outlined; right
/// click or Escape cancels.
/// </summary>
/// <remarks>
/// The UI takes the clicks before anyone else (<see cref="IInputManager.UIKeyBindStateChanged"/> is a notification, the
/// UI is the first to get it), so a button would react to the click that picks it. A transparent control that covers the
/// whole UI takes the click instead; the control under it is found with that control let the mouse through for a moment.
/// The outline is drawn after the UI (<see cref="IUserInterfaceManager.OnPostDrawUIRoot"/>): an
/// <see cref="Robust.Client.Graphics.Overlay"/> can't do it, overlays in the screen space are drawn into the viewport of
/// the game, under the windows and everything else of the UI.
/// </remarks>
public sealed class SuilControlPicker : IDisposable
{
    private static readonly Color Outline = Color.FromHex("#FFB000");

    private readonly IUserInterfaceManager _ui;
    private readonly IInputManager _input;
    private readonly Action<Control?> _done;
    private readonly CatchAll _catcher;

    private bool _active = true;
    private bool _disposed;
    private bool _pressed;
    private Control? _picked;

    public SuilControlPicker(IUserInterfaceManager ui, IInputManager input, Action<Control?> done)
    {
        _ui = ui;
        _input = input;
        _done = done;

        _catcher = new CatchAll(this);
        _ui.RootControl.AddChild(_catcher);
        _catcher.SetPositionLast();

        _ui.OnPostDrawUIRoot += OnPostDrawUIRoot;
        _input.UIKeyBindStateChanged += OnKeyBind;
    }

    /// <summary>
    /// Whether it still waits for a click.
    /// </summary>
    public bool IsActive => _active;

    /// <summary>
    /// Stops waiting: nothing is picked.
    /// </summary>
    public void Cancel() => Finish(null);

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _active = false;
        _ui.OnPostDrawUIRoot -= OnPostDrawUIRoot;
        _catcher.Orphan();

        // Dispose is called from the handler itself, and the input manager is walking the list of the handlers: taking
        // one out of it now leaves a hole that it calls (a NullReferenceException). The handler does nothing any more.
        Timer.Spawn(0, () => _input.UIKeyBindStateChanged -= OnKeyBind);
    }

    /// <summary>
    /// Escape and the right click cancel; they are taken, so the game does not get them. The left click is also seen
    /// here, so a pick does not depend on the click being routed to the control that covers the UI.
    /// </summary>
    private bool OnKeyBind(BoundKeyEventArgs args)
    {
        if (!_active)
            return false;

        if (args.Function == EngineKeyFunctions.UIClick)
        {
            if (args.State == BoundKeyState.Down)
                Press(args.PointerLocation);
            else
                Release();

            return true;
        }

        if (args.Function != EngineKeyFunctions.UIRightClick && args.Function != EngineKeyFunctions.EscapeMenu)
            return false;

        if (args.State == BoundKeyState.Down)
            Finish(null);

        return true;
    }

    private void Press(ScreenCoordinates position)
    {
        if (_pressed)
            return;

        _picked = Under(position);
        _pressed = true;
    }

    private void Release()
    {
        // the end of a click that started before the picking did is not a pick
        if (!_pressed)
            return;

        Finish(_picked);
    }

    private void Finish(Control? picked)
    {
        if (!_active)
            return;

        Dispose();
        _done(picked);
    }

    /// <summary>
    /// The control under the pointer, not counting the one that catches the clicks.
    /// </summary>
    private Control? Under(ScreenCoordinates position)
    {
        _catcher.MouseFilter = Control.MouseFilterMode.Ignore;

        try
        {
            return _ui.MouseGetControl(position);
        }
        finally
        {
            _catcher.MouseFilter = Control.MouseFilterMode.Stop;
        }
    }

    private void OnPostDrawUIRoot(PostDrawUIRootEventArgs args)
    {
        if (!_active)
            return;

        var hovered = Under(_input.MouseScreenPosition);

        if (hovered == null || hovered.Root != args.Root)
            return;

        var rect = hovered.GlobalPixelRect;
        args.DrawingHandle.DrawRect(rect, Outline.WithAlpha(0.25f));
        args.DrawingHandle.DrawRect(rect, Outline, filled: false);
    }

    /// <summary>
    /// Covers the UI and takes the clicks. The left click picks the control under it when the button is let go; a
    /// press that started before the picking did is not a pick.
    /// </summary>
    private sealed class CatchAll : Control
    {
        private readonly SuilControlPicker _owner;

        public CatchAll(SuilControlPicker owner)
        {
            _owner = owner;
            MouseFilter = MouseFilterMode.Stop;
            HorizontalExpand = true;
            VerticalExpand = true;
        }

        // Only the click is handled. A key has several functions (the left mouse button is also "use", "move the stored
        // item" and others, with a higher priority), and the first function that is handled stops the others: the click
        // would never come.
        protected override void KeyBindDown(GUIBoundKeyEventArgs args)
        {
            if (args.Function != EngineKeyFunctions.UIClick)
                return;

            args.Handle();
            _owner.Press(args.PointerLocation);
        }

        protected override void KeyBindUp(GUIBoundKeyEventArgs args)
        {
            if (args.Function != EngineKeyFunctions.UIClick)
                return;

            args.Handle();
            _owner.Release();
        }
    }
}
