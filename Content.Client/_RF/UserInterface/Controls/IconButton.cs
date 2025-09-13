using System.Numerics;
using Content.Client._RF.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._RF.UserInterface.Controls;

/// <summary>
/// Button with icon instead of text
/// </summary>
[Virtual]
public class IconButton : Button
{
    /// <summary>
    /// Path to icon texture
    /// </summary>
    public string TexturePath { set => _icon!.TexturePath = value; }

    /// <summary>
    /// Icon texture
    /// </summary>
    public Texture? Texture
    {
        get => _icon?.Texture;
        set => _icon!.Texture = value;
    }

    /// <summary>
    /// Icon texture scale
    /// </summary>
    public Vector2 TextureScale
    {
        get => _icon?.TextureScale ?? Vector2.Zero;
        set => _icon!.TextureScale = value;
    }

    private Color DefaultColor => HasStyleClass(StyleFortress.StyleClassButtonColorGold)
        ? StyleFortress.ButtonColorDefault
        : StyleFortress.GoldButtonColorDefault;

    private Color HoveredColor => HasStyleClass(StyleFortress.StyleClassButtonColorGold)
        ? StyleFortress.ButtonColorHovered
        : StyleFortress.GoldButtonColorHovered;

    private Color PressedColor => HasStyleClass(StyleFortress.StyleClassButtonColorGold)
        ? StyleFortress.ButtonColorPressed
        : StyleFortress.GoldButtonColorPressed;

    private Color DisabledColor => HasStyleClass(StyleFortress.StyleClassButtonColorGold)
        ? StyleFortress.ButtonColorDisabled
        : StyleFortress.GoldButtonColorDisabled;

    private readonly TextureRect? _icon;

    public IconButton()
    {
        _icon = new TextureRect
        {
            Stretch = TextureRect.StretchMode.KeepAspectCentered,
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            ModulateSelfOverride = DefaultColor,
            Margin = new Thickness(3f),
        };

        Label.Visible = false;

        AddChild(_icon);
    }

    protected override void DrawModeChanged()
    {
        base.DrawModeChanged();
        UpdateChildColors();
    }

    protected override void StylePropertiesChanged()
    {
        // colors of children depend on style, so ensure we update when style is changed
        base.StylePropertiesChanged();
        UpdateChildColors();
    }

    private void UpdateChildColors()
    {
        if (_icon == null)
            return;

        _icon.ModulateSelfOverride = DrawMode switch
        {
            DrawModeEnum.Normal => DefaultColor,
            DrawModeEnum.Hover => HoveredColor,
            DrawModeEnum.Pressed => PressedColor,
            DrawModeEnum.Disabled => DisabledColor,
            _ => _icon.ModulateSelfOverride,
        };
    }
}

