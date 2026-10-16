using System.Linq;
using Robust.Client.UserInterface;

namespace Content.Client._RF.UserInterface.Controls.TreeMenu;

public sealed partial class TreeMenu : RfBoxContainer
{
    public LayoutOrientation Orientation { get; set; }

    // The buttons whose events are subscribed, so that building the tree again doesn't do it twice.
    private readonly HashSet<TreeMenuButton> _built = new();

    public TreeMenu()
    {
        InitializeComponent();

        FlexDirection = Orientation.ToFlexDirection();
        RootPanel.Orientation = Orientation.Opposite();

        RootPanel.Content.OnChildAdded += control =>
        {
            if (control is TreeMenuButton button)
                BuildTree(button, RootPanel);
        };

        OnMouseExited += _ => RootPanel.ValidateVisibility();
    }

    private void BuildTree(TreeMenuButton button, TreeMenuPanel parent)
    {
        button.Content.Orientation = Orientation.Opposite();
        button.Content.ParentPanel = parent;

        // bruuuh
        // (the panel is in the button the first time and in the menu when the tree is built again, e.g. by the hot reload)
        button.Content.Orphan();
        AddChild(button.Content);

        if (!_built.Add(button))
            return;

        button.OnMouseEntered += _ =>
        {
            button.Content.Visible = button.Content.Content.ChildCount > 0;
            RootPanel.ValidateVisibility();
        };
        button.OnMouseExited += _ => RootPanel.ValidateVisibility();

        foreach (var child in button.Content.Content.Children.ToList())
        {
            if (child is TreeMenuButton buttonChild)
                BuildTree(buttonChild, button.Content);
        }
    }
}

/// <summary>
/// The direction of a tree menu, as it is written in XAML (<c>Orientation="Horizontal"</c>).
/// </summary>
public enum LayoutOrientation : byte
{
    Horizontal,
    Vertical,
}

public static class LayoutOrientationExtensions
{
    public static LayoutOrientation Opposite(this LayoutOrientation orientation)
    {
        return orientation == LayoutOrientation.Horizontal
            ? LayoutOrientation.Vertical
            : LayoutOrientation.Horizontal;
    }

    public static SuilFlexDirection ToFlexDirection(this LayoutOrientation orientation)
        => orientation == LayoutOrientation.Horizontal ? SuilFlexDirection.Row : SuilFlexDirection.Column;

    public static LayoutOrientation ToOrientation(this SuilFlexDirection direction)
        => direction is SuilFlexDirection.Column or SuilFlexDirection.ColumnReverse
            ? LayoutOrientation.Vertical
            : LayoutOrientation.Horizontal;
}
