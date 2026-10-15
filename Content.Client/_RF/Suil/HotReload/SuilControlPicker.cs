using Robust.Client.Input;
using Robust.Client.UserInterface;
using Robust.Shared.Input;

namespace Content.Client._RF.Suil.HotReload;

/// <summary>
/// Waits for a click on a control of the UI and tells which one it was. The control under the mouse is outlined; right
/// click or Escape cancels.
/// </summary>
/// <remarks>
/// The clicks are taken before the UI gets them (<see cref="IInputManager.UIKeyBindStateChanged"/>), so the control that is
/// picked doesn't react to the click, and the outline is drawn after the UI
/// (<see cref="IUserInterfaceManager.OnPostDrawUIRoot"/>). A control that covers the screen would be in the way of the
/// first, and an <see cref="Robust.Client.Graphics.Overlay"/> can't do the second: overlays in the screen space are drawn
/// into the viewport of the game, under the windows and everything else of the UI.
/// </remarks>
public sealed class SuilControlPicker : IDisposable
{
    private static readonly Color Outline = Color.FromHex("#FFB000");

    private readonly IUserInterfaceManager _ui;
    private readonly IInputManager _input;
    private readonly Action<Control?> _done;

    // the button that picked: its release is taken as well, or the control under the mouse would get a release without a press
    private BoundKeyFunction? _waitingForRelease;
    private Control? _picked;
    private bool _active = true;

    public SuilControlPicker(IUserInterfaceManager ui, IInputManager input, Action<Control?> done)
    {
        _ui = ui;
        _input = input;
        _done = done;

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
        _ui.OnPostDrawUIRoot -= OnPostDrawUIRoot;
        _input.UIKeyBindStateChanged -= OnKeyBind;
        _active = false;
    }

    private bool OnKeyBind(BoundKeyEventArgs args)
    {
        if (!_active)
            return false;

        var function = args.Function;

        if (_waitingForRelease != null)
        {
            if (function != _waitingForRelease)
                return false;

            if (args.State != BoundKeyState.Up)
                return true;

            // the click is over, now the picked control is handed over
            var picked = _picked;
            Dispose();
            _done(picked);
            return true;
        }

        if (function != EngineKeyFunctions.UIClick
            && function != EngineKeyFunctions.UIRightClick
            && function != EngineKeyFunctions.EscapeMenu)
        {
            return false;
        }

        if (args.State != BoundKeyState.Down)
            return true; // the end of a click that started before the picking did

        if (function == EngineKeyFunctions.UIClick)
        {
            _picked = _ui.MouseGetControl(args.PointerLocation);
            _waitingForRelease = function;
            return true;
        }

        _waitingForRelease = function;
        _picked = null;
        return true;
    }

    private void Finish(Control? picked)
    {
        if (!_active)
            return;

        Dispose();
        _done(picked);
    }

    private void OnPostDrawUIRoot(PostDrawUIRootEventArgs args)
    {
        if (!_active || _waitingForRelease != null)
            return;

        var hovered = _ui.MouseGetControl(_input.MouseScreenPosition);

        if (hovered == null || hovered.Root != args.Root)
            return;

        var rect = hovered.GlobalPixelRect;
        args.DrawingHandle.DrawRect(rect, Outline.WithAlpha(0.25f));
        args.DrawingHandle.DrawRect(rect, Outline, filled: false);
    }
}
