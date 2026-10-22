using System.Numerics;
using Content.Client._RF.UserInterface.Scope;
using Content.Client.Guidebook;
using Content.Client.Guidebook.Components;
using Content.Shared.Guidebook;
using Content.Suil;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._RF.UserInterface.Controls;

/// <summary>
/// A window that can be opened, moved and resized, the SUIL version of <c>FancyWindow</c>: the <see cref="BaseWindow"/>
/// logic with the look of <see cref="RfWindowChrome"/>, which is styled by the stylesheets of SUIL.
/// </summary>
/// <remarks>
/// The children of the window go to its contents, so in SUIL the tree of the window is written right inside it.
/// </remarks>
[Virtual]
[SuilTag("window")]
public partial class RfWindow : BaseWindow
{
    [Dependency] private IEntitySystemManager _sysMan = default!;

    private const int DragMarginSize = 7;

    private readonly RfWindowChrome _chrome = new();
    private GuidebookSystem? _guidebook;
    private UiScope? _scope;

    public RfWindow()
    {
        IoCManager.InjectDependencies(this);

        MouseFilter = MouseFilterMode.Stop;
        MinSize = new Vector2(200, 150);

        AddChild(_chrome);
        XamlChildren = _chrome.Body.Children;

        _chrome.OnCloseRequested += Close;
        _chrome.OnHelpRequested += Help;
    }

    /// <summary>
    /// What the window does while it is in the tree: listens to events, updates itself at an interval. See
    /// <see cref="UiScope"/>.
    /// </summary>
    public UiScope Scope => _scope ??= new UiScope(IsInsideTree);

    public string? Title
    {
        get => _chrome.Title.Text;
        set => _chrome.Title.Text = value;
    }

    /// <summary>
    /// The control the children of the window are put in.
    /// </summary>
    public Control Contents => _chrome.Body;

    /// <summary>
    /// Whether the button that closes the window is shown.
    /// </summary>
    public bool CloseButtonVisible
    {
        get => _chrome.Close.Visible;
        set => _chrome.Close.Visible = value;
    }

    public List<ProtoId<GuideEntryPrototype>>? HelpGuidebookIds
    {
        get;
        set
        {
            field = value;
            _chrome.Help.Disabled = value == null;
            _chrome.Help.Visible = value != null;
        }
    }

    public void Help()
    {
        if (HelpGuidebookIds is null)
            return;

        _guidebook ??= _sysMan.GetEntitySystem<GuidebookSystem>();
        _guidebook.OpenHelp(HelpGuidebookIds);
    }

    [ProxyFor(typeof(IUserInterfaceManager), nameof(IUserInterfaceManager.GetUIController))]
    protected T Controller<T>() where T : UIController, new() => UserInterfaceManager.GetUIController<T>();

    protected override void EnteredTree()
    {
        base.EnteredTree();
        _scope?.Enter();
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();
        _scope?.Exit();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        _scope?.Update(args.DeltaSeconds, VisibleInTree);
    }

    protected override DragMode GetDragModeFor(Vector2 relativeMousePos)
    {
        var mode = DragMode.Move;

        if (!Resizable)
            return mode;

        if (relativeMousePos.Y < DragMarginSize)
            mode = DragMode.Top;
        else if (relativeMousePos.Y > Size.Y - DragMarginSize)
            mode = DragMode.Bottom;

        if (relativeMousePos.X < DragMarginSize)
            mode |= DragMode.Left;
        else if (relativeMousePos.X > Size.X - DragMarginSize)
            mode |= DragMode.Right;

        return mode;
    }
}

public static class RfWindowExt
{
    /// <summary>
    /// Sets the title of a window to the name of an entity and its guidebooks to those of the entity.
    /// </summary>
    public static void SetInfoFromEntity(this RfWindow window, IEntityManager entityManager, EntityUid entity)
    {
        window.Title = entityManager.GetComponent<MetaDataComponent>(entity).EntityName;
        window.HelpGuidebookIds = entityManager.GetComponentOrNull<GuideHelpComponent>(entity)?.Guides;
    }
}
