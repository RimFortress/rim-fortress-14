using Content.Client._RF.UserInterface.Controls;

namespace Content.Client._RF.ActionsHotbar.UI;

/// <summary>
/// Separator between the tree menu panels.
/// It exists only to simplify the logic of opening/closing panels
/// </summary>
public sealed partial class TreeMenuSeparator : RfBoxContainer
{
    public TreeMenuSeparator()
    {
        InitializeComponent();
    }
}
