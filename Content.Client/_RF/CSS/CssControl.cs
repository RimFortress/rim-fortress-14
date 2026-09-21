using Content.Client._RF.Graphics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Utility;

namespace Content.Client._RF.CSS;

/// <summary>
/// The <see cref="Control"/> version supports setting style parameters via CSS.
/// You should always use this class instead of <see cref="Control"/> when creating a UI for RimFortress.
/// </summary>
/// <remarks>
/// To allow the field's value to be set via CSS, you need to mark it using the <see cref="CssPropertyAttribute"/>.
/// </remarks>
[Virtual]
public partial class CssControl : Control
{
    /// <summary>
    /// Is this control the <see cref="UserInterfaceManager.CurrentlyHovered"/>?
    /// </summary>
    public bool Hovered { get; private set; }

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
        var properties = new Dictionary<string, CssPropertyAccessor>();

        Register(nameof(BackgroundColor), (c, v) => c.BackgroundColor = v, c => c.BackgroundColor);
        Register(nameof(BackgroundImage), (c, v) => c.BackgroundImage = v, c => c.BackgroundImage);
        Register(nameof(BackgroundRepeat), (c, v) => c.BackgroundRepeat = v, c => c.BackgroundRepeat);
        Register(nameof(BackgroundSize), (c, v) => c.BackgroundSize = v, c => c.BackgroundSize);
        Register(nameof(BackgroundPositionX), (c, v) => c.BackgroundPositionX = v, c => c.BackgroundPositionX);
        Register(nameof(BackgroundPositionY), (c, v) => c.BackgroundPositionY = v, c => c.BackgroundPositionY);

        return properties;

        void Register<TField>(
            string fieldName,
            Action<CssControl, TField> setter,
            Func<CssControl, TField> getter,
            Type? handler = null)
        {
            var accessor = new CssPropertyAccessor(typeof(TField),
                (c, v) => setter((CssControl)c, (TField)v),
                control => getter((CssControl)control)!,
                handler);

            properties.Add(CaseConversion.PascalToKebab(fieldName), accessor);
        }
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
        Hovered = true;
        DoStyleUpdate();
    }

    protected override void MouseExited()
    {
        base.MouseExited();
        Hovered = false;
        DoStyleUpdate();
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        DrawBackground(handle);
    }

    protected override void StylePropertiesChanged()
    {
        base.StylePropertiesChanged();

        BackgroundColor = TryGetStyleProperty<Color?>("background-color", out var color) ? color : null;
        BackgroundImage = TryGetStyleProperty<Texture?>("background-image", out var texture) ? texture : null;
        BackgroundRepeat = TryGetStyleProperty<RfStyleBoxTexture.StretchMode>("background-repeat", out var mode)
            ? mode
            : RfStyleBoxTexture.StretchMode.Repeat;
        BackgroundSize = TryGetStyleProperty<RfStyleBoxTexture.StyleBoxSize>("background-size", out var size)
            ? size
            : RfStyleBoxTexture.StyleBoxSize.Auto;
        BackgroundPositionX = TryGetStyleProperty<StyleBoxPositionComponent>("background-position-x", out var positionX)
            ? positionX
            : StyleBoxPositionComponent.Zero;
        BackgroundPositionY = TryGetStyleProperty<StyleBoxPositionComponent>("background-position-y", out var positionY)
            ? positionY
            : StyleBoxPositionComponent.Zero;
    }
}
