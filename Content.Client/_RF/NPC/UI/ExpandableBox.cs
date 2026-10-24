using Content.Client._RF.UserInterface.Controls;
using Content.Client.Resources;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Input;

namespace Content.Client._RF.NPC.UI;

/// <summary>
/// A container whose contents can be hidden or revealed by clicking.
/// </summary>
public sealed partial class ExpandableBox : RfBoxContainer
{
    private const string OpenIcon = "/Textures/_RF/Interface/chevron-down-solid-full.svg.192dpi.png";
    private const string FoldedIcon = "/Textures/_RF/Interface/chevron-right-solid.svg.192dpi.png";

    [Dependency] private IResourceCache _cache = default!;

    private readonly Texture _openTexture;
    private readonly Texture _foldedTexture;

    /// <summary>
    /// A title (markup) that will be displayed even if the container is hidden.
    /// </summary>
    [PublicAPI]
    public string? Title
    {
        get => TitleLabel.Text;
        set => TitleLabel.Text = value;
    }

    [PublicAPI]
    public string? SecondaryText
    {
        get => SecondaryLabel.Text;
        set => SecondaryLabel.Text = value;
    }

    [PublicAPI]
    public bool Expanded
    {
        get => Content.Visible;
        set => SetExpanded(value);
    }

    public ExpandableBox()
    {
        IoCManager.InjectDependencies(this);
        InitializeComponent();

        _openTexture = _cache.GetTexture(OpenIcon);
        _foldedTexture = _cache.GetTexture(FoldedIcon);

        TitleBox.OnKeyBindDown += args =>
        {
            if (args.Function == EngineKeyFunctions.Use
                || args.Function == EngineKeyFunctions.UseSecondary)
                SetExpanded(!Expanded);
        };

        SetExpanded(Expanded);
    }

    private void SetExpanded(bool expanded)
    {
        Marker.BackgroundImage = expanded ? _openTexture : _foldedTexture;
        Content.Visible = expanded;
    }

    public void ExpandAll()
    {
        Expanded = true;
        var parent = Parent;

        while (parent != null)
        {
            if (parent is ExpandableBox box)
                box.Expanded = true;

            parent = parent.Parent;
        }
    }

    public void CollapseAll()
    {
        var queue = new Queue<Robust.Client.UserInterface.Control>();
        queue.Enqueue(this);

        while (queue.TryDequeue(out var control))
        {
            if (control is ExpandableBox box)
                box.Expanded = false;

            foreach (var child in control.Children)
            {
                queue.Enqueue(child);
            }
        }
    }
}
