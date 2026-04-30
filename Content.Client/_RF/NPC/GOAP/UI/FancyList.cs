using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;

namespace Content.Client._RF.NPC.GOAP.UI;

/// <summary>
///
/// </summary>
[Virtual]
public class FancyList : BoxContainer
{
    public const string OddRowStyleClass = "item-odd-row";
    public const string EvenRowStyleClass = "item-even-row";

    public FancyList()
    {
        RobustXamlLoader.Load(this);
        Orientation = LayoutOrientation.Horizontal;
    }

    protected override void ChildAdded(Control newChild)
    {
        base.ChildAdded(newChild);
        RefreshStyles();
    }

    protected override void ChildRemoved(Control child)
    {
        base.ChildRemoved(child);
        RefreshStyles();
    }

    protected override void ChildMoved(Control child, int oldIndex, int newIndex)
    {
        base.ChildMoved(child, oldIndex, newIndex);
        RefreshStyles();
    }

    private void RefreshStyles()
    {
        var even = true;

        foreach (var control in Children)
        {
            if (even)
            {
                control.AddStyleClass(EvenRowStyleClass);
                control.RemoveStyleClass(OddRowStyleClass);
            }
            else
            {
                control.AddStyleClass(OddRowStyleClass);
                control.RemoveStyleClass(EvenRowStyleClass);
            }

            even = !even;
        }
    }
}

