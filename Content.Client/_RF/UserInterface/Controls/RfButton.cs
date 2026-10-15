using System.Linq;
using Content.Client._RF.Suil;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Input;

namespace Content.Client._RF.UserInterface.Controls;

/// <summary>
/// Base class for a generic SUIL-styled button, the <see cref="UiElement"/> version of
/// <see cref="Robust.Client.UserInterface.Controls.BaseButton"/>.
/// </summary>
/// <remarks>
/// There is no draw mode: the state is exposed to stylesheets as the <c>:hover</c>, <c>:active</c>,
/// <c>:disabled</c> and <c>:checked</c> pseudo-classes.
/// </remarks>
[Virtual, UsedImplicitly]
public class RfButton : UiElement
{
    /// <summary>
    /// Name of the pseudo-class (<c>:checked</c>) set while the button is <see cref="Pressed"/>.
    /// </summary>
    private int _attemptingPress;

    /// <summary>
    /// The group this button belongs to. Of multiple buttons in the same group, only one can be pressed (radio buttons).
    /// </summary>
    [PublicAPI]
    public RfButtonGroup? Group
    {
        get;
        set
        {
            if (value?.InternalButtons.Contains(this) ?? false)
                return;

            field?.InternalButtons.Remove(this);
            field = value;

            if (value == null)
                return;

            value.InternalButtons.Add(this);
            ToggleMode = true;

            if (value.IsNoneSetAllowed)
            {
                // Still unpress if there's another pressed button, but don't press it otherwise.
                if (value.Pressed != this)
                    Pressed = false;
            }
            else
            {
                // The first button of the group is pressed. Doesn't go through the setter to avoid its own check.
                Pressed = value.InternalButtons.Count == 1;
            }

            StateChanged();
        }
    }

    /// <summary>
    /// Controls mode of operation in relation to press/release events.
    /// </summary>
    [PublicAPI, ViewVariables]
    public ActionMode Mode { get; set; } = ActionMode.Release;

    /// <summary>
    /// Whether the button is currently toggled down. Only applies when <see cref="ToggleMode"/> is true.
    /// </summary>
    [PublicAPI, ViewVariables]
    public bool Pressed
    {
        get;
        set
        {
            if (field == value)
                return;

            if (!value && Group is { IsNoneSetAllowed: false })
            {
                throw new InvalidOperationException(
                    "Cannot directly unset a grouped button. Set another button in the group instead.");
            }

            field = value;

            if (Group != null && field)
                UnsetOtherGroupButtons();

            StateChanged();
        }
    }

    /// <summary>
    /// Whether key functions other than <see cref="EngineKeyFunctions.UIClick"/> trigger the button.
    /// </summary>
    [PublicAPI, ViewVariables]
    public bool EnableAllKeybinds { get; set; }

    /// <summary>
    /// If <c>true</c>, this button functions as a toggle, not as a regular push button.
    /// </summary>
    [PublicAPI, ViewVariables]
    public bool ToggleMode
    {
        get;
        set
        {
            if (Group != null && !value)
                throw new InvalidOperationException("Cannot disable toggle mode on a button in a group.");

            field = value;
        }
    }

    /// <summary>
    /// If <c>true</c>, this button will not emit sounds when the mouse is pressed or hovered over.
    /// </summary>
    [PublicAPI, ViewVariables]
    public bool MuteSounds { get; set; }

    /// <summary>
    /// Fired when the button is pushed down by the mouse.
    /// </summary>
    [PublicAPI]
    public event Action<RfButtonEventArgs>? OnButtonDown;

    /// <summary>
    /// Fired when the button is released by the mouse.
    /// </summary>
    [PublicAPI]
    public event Action<RfButtonEventArgs>? OnButtonUp;

    /// <summary>
    /// Fired when the button is "pressed". When this happens depends on <see cref="Mode"/>.
    /// </summary>
    [PublicAPI]
    public event Action<RfButtonEventArgs>? OnPressed;

    /// <summary>
    /// If <see cref="ToggleMode"/> is set, fired when the button is toggled up or down.
    /// </summary>
    [PublicAPI]
    public event Action<RfButtonToggledEventArgs>? OnToggled;

    /// <inheritdoc/>
    protected override bool CenterTextVertically => true;

    public RfButton()
    {
        MouseFilter = MouseFilterMode.Stop;
    }

    /// <summary>
    /// Sets the button's press state and also handles click sounds.
    /// </summary>
    [PublicAPI]
    public void SetClickPressed(bool value)
    {
        Pressed = value;

        if (Pressed != value)
            return;

        if (!MuteSounds)
            UserInterfaceManager.ClickSound();
    }

    /// <summary>
    /// Called whenever the pressed state of the button changes, after the pseudo-classes have been updated.
    /// </summary>
    protected virtual void StateChanged()
    {
        SetSuilPseudoStyleClass(SuilPseudoClass.Checked, Pressed);
        Active = _attemptingPress > 0;
    }

    protected override void OnDisabledChanged()
    {
        base.OnDisabledChanged();

        if (Disabled)
            _attemptingPress = 0;

        StateChanged();
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (IsIgnoredKey(args))
            return;

        var buttonEventArgs = new RfButtonEventArgs(this, args);
        OnButtonDown?.Invoke(buttonEventArgs);

        if (Mode == ActionMode.Release)
        {
            UserInterfaceManager.ControlFocused = this;
            _attemptingPress += 1;
        }
        else if (ToggleMode)
        {
            // Can't unpress a radio button directly.
            if (Group == null || !Pressed)
            {
                SetClickPressed(!Pressed);
                OnPressed?.Invoke(buttonEventArgs);
                OnToggled?.Invoke(new RfButtonToggledEventArgs(Pressed, this, args));
                UnsetOtherGroupButtons();
            }
        }
        else
        {
            UserInterfaceManager.ControlFocused = this;
            _attemptingPress += 1;
            OnPressed?.Invoke(buttonEventArgs);
        }

        StateChanged();
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);

        if (IsIgnoredKey(args))
            return;

        var buttonEventArgs = new RfButtonEventArgs(this, args);
        OnButtonUp?.Invoke(buttonEventArgs);

        if (Mode == ActionMode.Release && _attemptingPress > 0 && HasPoint((args.PointerLocation.Position - GlobalPixelPosition) / UIScale))
        {
            // Only trigger toggle on UIClick. Do not unpress a toggle button if it's in a group.
            if (args.Function != EngineKeyFunctions.UIClick || Group == null || !Pressed)
            {
                if (args.Function == EngineKeyFunctions.UIClick && ToggleMode && _attemptingPress == 1)
                {
                    SetClickPressed(!Pressed);
                }
                else if (!MuteSounds)
                {
                    UserInterfaceManager.ClickSound();
                }

                OnPressed?.Invoke(buttonEventArgs);

                if (args.Function == EngineKeyFunctions.UIClick && ToggleMode)
                {
                    OnToggled?.Invoke(new RfButtonToggledEventArgs(Pressed, this, args));

                    if (Pressed)
                        UnsetOtherGroupButtons();
                }
            }
        }

        if (_attemptingPress > 0)
            _attemptingPress -= 1;

        if (_attemptingPress <= 0 && UserInterfaceManager.ControlFocused == this)
            UserInterfaceManager.ControlFocused = null;

        StateChanged();
    }

    protected override void ControlFocusExited()
    {
        base.ControlFocusExited();

        _attemptingPress = 0;
        StateChanged();
    }

    protected override void MouseEntered()
    {
        base.MouseEntered();

        if (!Disabled && !MuteSounds)
            UserInterfaceManager.HoverSound();
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();

        Group = null;
    }

    private bool IsIgnoredKey(GUIBoundKeyEventArgs args)
        => Disabled
           || args.Function == EngineKeyFunctions.Use
           || !EnableAllKeybinds && args.Function != EngineKeyFunctions.UIClick;

    private void UnsetOtherGroupButtons()
    {
        if (Group == null)
            return;

        foreach (var button in Group.InternalButtons)
        {
            if (button == this || !button.Pressed)
                continue;

            button.Pressed = false;
            button.StateChanged();
        }
    }

    /// <summary>
    /// For use with <see cref="Mode"/>.
    /// </summary>
    public enum ActionMode : byte
    {
        /// <summary>
        /// <see cref="OnPressed"/> fires when the mouse button causing them is pressed down.
        /// </summary>
        Press = 0,

        /// <summary>
        /// <see cref="OnPressed"/> fires when the mouse button causing them is released.
        /// This is the default and most intuitive method.
        /// </summary>
        Release = 1,
    }
}

[Virtual]
public class RfButtonEventArgs(RfButton button, GUIBoundKeyEventArgs args) : EventArgs
{
    /// <summary>
    /// The button this event originated from.
    /// </summary>
    public RfButton Button { get; } = button;

    public GUIBoundKeyEventArgs Event { get; } = args;
}

/// <summary>
/// Fired when a <see cref="RfButton"/> is toggled.
/// </summary>
public sealed class RfButtonToggledEventArgs(bool pressed, RfButton button, GUIBoundKeyEventArgs args)
    : RfButtonEventArgs(button, args)
{
    /// <summary>
    /// The new pressed state of the button.
    /// </summary>
    public bool Pressed { get; } = pressed;
}

/// <summary>
/// Represents a group of buttons. Of all buttons in a group, only one can be pressed down (radio buttons).
/// </summary>
public sealed class RfButtonGroup(bool isNoneSetAllowed = true)
{
    /// <summary>
    /// Whether it is legal for this button group to have no selected button.
    /// </summary>
    public bool IsNoneSetAllowed { get; } = isNoneSetAllowed;

    internal readonly List<RfButton> InternalButtons = new();

    public IReadOnlyList<RfButton> Buttons => InternalButtons;

    public RfButton? Pressed => InternalButtons.FirstOrDefault(x => x.Pressed);
}
