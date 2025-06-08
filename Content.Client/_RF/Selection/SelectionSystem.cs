using Content.Shared._RF.NPC;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Client._RF.Selection;

public sealed class SelectionSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly IOverlayManager _overlay = default!;

    /// <summary>
    /// Selection frame start point
    /// </summary>
    public MapCoordinates? StartPoint { get; private set; }

    /// <summary>
    /// Selection frame endpoint
    /// </summary>
    public MapCoordinates? EndPoint { get; private set; }

    /// <summary>
    /// Entities within the boundaries of the selection frame
    /// </summary>
    public HashSet<EntityUid> Selected { get; private set; } = new();

    /// <summary>
    /// Selection drawing color
    /// </summary>
    public Color SelectionColor = Color.White;

    /// <summary>
    /// A function that filters entities for selection
    /// </summary>
    private Func<EntityUid, bool>? _selectionFilter;

    /// <summary>
    /// Action taken when the selection is completed
    /// </summary>
    private Action<HashSet<EntityUid>>? _onSelected;

    /// <summary>
    /// The action performed on selected entities when the right mouse button is pressed
    /// </summary>
    private Action<(HashSet<EntityUid> Selected, EntityUid? ActUid, EntityCoordinates ActCoords)>? _act;

    /// <summary>
    /// An icon that will be drawn next to the mouse cursor
    /// </summary>
    public string? IconPath { get; private set; }

    /// <summary>
    /// Color of the icon that will be drawn next to the mouse cursor
    /// </summary>
    public Color IconColor { get; private set; } = Color.White;

    public event Action? OnUpdateSelection;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        _overlay.AddOverlay(new SelectionOverlay());

        CommandBinds.Builder
            .Bind(EngineKeyFunctions.Use, new PointerStateInputCmdHandler(OnSelectEnabled, OnSelectDisabled))
            .Bind(EngineKeyFunctions.UseSecondary, new PointerInputCmdHandler(OnUseSecondary))
            .Register<SharedNpcControlSystem>();
    }

    private bool OnSelectEnabled(ICommonSession? player, EntityCoordinates coords, EntityUid uid)
    {
        Selected.Clear();

        StartPoint = _transform.ToMapCoordinates(coords);
        EndPoint = StartPoint;
        return false;
    }

    private bool OnSelectDisabled(ICommonSession? player, EntityCoordinates coords, EntityUid uid)
    {
        _onSelected?.Invoke(Selected);

        StartPoint = null;
        EndPoint = null;
        return false;
    }

    private bool OnUseSecondary(ICommonSession? player, EntityCoordinates coords, EntityUid uid)
    {
        if (Selected.Count == 0)
            return false;

        _act?.Invoke((Selected, uid.IsValid() ? uid : null, coords));
        return true;
    }

    /// <summary>
    /// Gets the list of entities in the selection area
    /// </summary>
    private HashSet<EntityUid> EntitiesInSelect()
    {
        if (StartPoint is not { } start
            || EndPoint is not { } end
            || start.MapId != end.MapId)
            return new HashSet<EntityUid>();

        var area = new Box2(start.Position, end.Position);
        var entities = _lookup.GetEntitiesIntersecting(start.MapId, area);

        if (_selectionFilter == null)
            return entities;

        foreach (var entity in entities)
        {
            if (!_selectionFilter(entity))
                entities.Remove(entity);
        }

        return entities;
    }

    public void SetSelection(
        Action<(HashSet<EntityUid> Selected, EntityUid? ActUid, EntityCoordinates ActCoords)>? act = null,
        Color? color = null,
        Func<EntityUid, bool>? filter = null,
        Action<HashSet<EntityUid>>? onSelected = null,
        string? iconPath = null,
        Color? iconColor = null)
    {
        SetDefault();

        SelectionColor = color ?? Color.LightGray;
        _selectionFilter = filter;
        _onSelected = onSelected;
        IconPath = iconPath;
        IconColor = iconColor ?? Color.LightGray;
        _act = act;

        OnUpdateSelection?.Invoke();
    }

    private void SetDefault()
    {
        _selectionFilter = null;
        _onSelected = null;
        IconPath = null;
        IconColor = Color.LightGray;
        _act = null;

        Selected.Clear();
    }

    public void Select(EntityUid uid)
    {
        Selected.Add(uid);
    }

    public void DeSelect(EntityUid uid)
    {
        Selected.Remove(uid);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (StartPoint is not { } start
            || EndPoint is not { } end
            || start.MapId != end.MapId)
        {
            StartPoint = null;
            EndPoint = null;
            return;
        }

        if (_input.MouseScreenPosition is { IsValid: true } mousePos)
            EndPoint = _eye.PixelToMap(mousePos);

        Selected = EntitiesInSelect();
    }
}
