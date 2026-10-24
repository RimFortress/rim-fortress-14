using Content.Client._RF.UserInterface.Controls;
using Robust.Client.Graphics;

namespace Content.Client._RF.Construction.Controls;

/// <summary>
/// A step of the guide of a building: the icon and the text.
/// </summary>
public sealed partial class ConstructionStep : RfBoxContainer
{
    public ConstructionStep(string text, Texture? icon, int depth)
    {
        InitializeComponent();

        TextLabel.SetPlainText(text);
        Icon.Texture = icon;

        // the steps inside of other steps are shifted to the right
        Margin = new Thickness(depth * 8f, 0, 0, 0);
    }
}
