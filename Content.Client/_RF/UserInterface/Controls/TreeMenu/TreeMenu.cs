using Robust.Client.UserInterface;

namespace Content.Client._RF.UserInterface.Controls.TreeMenu;

public sealed partial class TreeMenu : RfBoxContainer
{
    public LayoutOrientation Orientation { get; set; }

    public TreeMenu()
    {
        InitializeComponent();
        XamlChildren = RootPanel.XamlChildren;

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
        button.RemoveChild(button.Content);
        AddChild(button.Content);

        button.OnMouseEntered += _ =>
        {
            button.Content.Visible = button.Content.Content.ChildCount > 0;
            RootPanel.ValidateVisibility();
        };
        button.OnMouseExited += _ => RootPanel.ValidateVisibility();

        foreach (var child in button.Content.Content.Children)
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
