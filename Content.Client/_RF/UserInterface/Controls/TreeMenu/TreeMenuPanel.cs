using Robust.Client.Input;
using Robust.Client.UserInterface;

namespace Content.Client._RF.UserInterface.Controls.TreeMenu;

public sealed partial class TreeMenuPanel : RfBoxContainer
{
    [Dependency] private IInputManager _input = default!;

    public bool Closable { get; set; } = true;
    public TreeMenuButton? ParentButton { get; internal set; }
    public TreeMenuPanel? ParentPanel { get; internal set; }

    /// <summary>
    /// The direction of the buttons of the panel; the panel itself goes the other way, so that the gap is on the side
    /// the next panel opens to.
    /// </summary>
    public LayoutOrientation Orientation
    {
        get => Content.FlexDirection.ToOrientation();
        set
        {
            FlexDirection = value.Opposite().ToFlexDirection();
            Content.FlexDirection = value.ToFlexDirection();
        }
    }

    public TreeMenuPanel()
    {
        IoCManager.InjectDependencies(this);
        InitializeComponent();

        XamlChildren = Content.Children;
    }

    /// <summary>
    /// Deletes all the contents of this panel
    /// </summary>
    public void Clear()
    {
        Content.RemoveAllChildren();
    }

    /// <summary>
    /// Hides this panel and all child panels
    /// </summary>
    public void Collapse()
    {
        if (!Closable || !Visible)
            return;

        Visible = false;

        foreach (var child in Content.Children)
        {
            if (child is TreeMenuButton button)
                button.Content.Collapse();
        }
    }

    /// <summary>
    /// Perform a visibility check for this panel and all child panels
    /// </summary>
    public void ValidateVisibility()
    {
        if (UserInterfaceManager.MouseGetControl(_input.MouseScreenPosition) is TreeMenuButton hovered
            && ParentPanel?.Content.Children.Contains(hovered) == true
            && ParentButton != hovered)
            Collapse();

        var point = (Vector2i) _input.MouseScreenPosition.Position;
        var canCollapse = true;

        foreach (var child in Content.Children)
        {
            if (child is not TreeMenuButton button)
                continue;

            if (button == ParentButton && button.GlobalPixelRect.Contains(point))
                continue;

            if (button.Content.Content.ChildCount <= 0
                || !button.Content.Visible)
                continue;

            button.Content.ValidateVisibility();

            if (button.Content.Visible)
                canCollapse = false;
        }

        if (!canCollapse
            || ParentPanel?.GlobalPixelRect.Contains(point) == true
            || GlobalPixelRect.Contains(point))
            return;

        Collapse();
    }
}

