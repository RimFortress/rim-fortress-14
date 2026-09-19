using Robust.Client.UserInterface;

namespace Content.Client._RF.CSS;

/// <summary>
/// The <see cref="Control"/> version supports setting style parameters via CSS.
/// You should always use this class instead of <see cref="Control"/> when creating a UI for RimFortress.
/// </summary>
/// <remarks>
/// To allow the field's value to be set via CSS, you need to mark it using the <see cref="CssPropertyAttribute"/>.
/// </remarks>
[Virtual]
public class CssControl : Control
{
    /// <summary>
    /// Is this control the <see cref="UserInterfaceManager.CurrentlyHovered"/>?
    /// </summary>
    public bool Hovered => UserInterfaceManager.CurrentlyHovered == this;

    /// <summary>
    /// The method through which CssPropertiesGenerator adds
    /// CSS properties extracted from this class's <see cref="CssPropertyAttribute"/>,
    /// and through which <see cref="ICssManager"/> then builds the style sheet.
    /// </summary>
    /// <remarks>
    /// You should never manually modify the contents of this method.
    /// </remarks>
    public virtual Dictionary<string, CssPropertyAccessor> AutoCssProperties()
    {
        return new();
    }

    protected override void ChildMoved(Control child, int oldIndex, int newIndex)
    {
        base.ChildMoved(child, oldIndex, newIndex);

        foreach (var control in Children)
        {
            control.DoStyleUpdate();
        }
    }

    protected override void ChildAdded(Control newChild)
    {
        base.ChildAdded(newChild);

        foreach (var control in Children)
        {
            if (control != newChild)
                control.DoStyleUpdate();
        }
    }

    protected override void ChildRemoved(Control child)
    {
        base.ChildRemoved(child);

        foreach (var control in Children)
        {
            if (control != child)
                control.DoStyleUpdate();
        }
    }

    protected override void KeyboardFocusEntered()
    {
        base.KeyboardFocusEntered();
        DoStyleUpdate();
    }

    protected override void ControlFocusExited()
    {
        base.ControlFocusExited();
        DoStyleUpdate();
    }

    protected override void KeyboardFocusExited()
    {
        base.KeyboardFocusExited();
        DoStyleUpdate();
    }

    protected override void MouseEntered()
    {
        base.MouseEntered();
        DoStyleUpdate();
    }

    protected override void MouseExited()
    {
        base.MouseExited();
        DoStyleUpdate();
    }
}
