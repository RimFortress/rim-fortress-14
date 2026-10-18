using Content.Suil;
using System.Linq;
using Content.Client._RF.UserInterface.Controls;
using Content.Client.Guidebook.Controls;
using Content.Client.Humanoid;
using Content.Client.UserInterface.ControlExtensions;
using Content.Shared.Body;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._RF.Lobby.UI;

/// <summary>
/// The markings of one layer: a search field, a grid of <see cref="RfLayerMarkingItem"/> to choose from (or the
/// <see cref="RfLayerMarkingOrderer"/> while reordering) and the limits of the layer. The
/// <see cref="RfBoxContainer"/> version of <see cref="LayerMarkingPicker"/>.
/// </summary>
public sealed partial class RfLayerMarkingPicker : RfBoxContainer
{
    private const int ColumnWidth = 500;
    private const long FrameBudgetMs = 6;

    private readonly IReadOnlyDictionary<string, MarkingPrototype> _allMarkings;
    private readonly ProtoId<OrganCategoryPrototype> _organ;
    private readonly HumanoidVisualLayers _layer;
    private readonly MarkingsViewModel _markingsModel;
    private List<ISearchableControl> _searchable = new();
    private Queue<MarkingPrototype>? _pending;
    private readonly System.Diagnostics.Stopwatch _stopwatch = new();

    public RfLayerMarkingPicker(
        MarkingsViewModel markingsModel,
        ProtoId<OrganCategoryPrototype> organ,
        HumanoidVisualLayers layer,
        IReadOnlyDictionary<string, MarkingPrototype> allMarkings)
    {
        InitializeComponent();

        _markingsModel = markingsModel;
        _allMarkings = allMarkings;
        _organ = organ;
        _layer = layer;

        OrderingItems.AddChild(new RfLayerMarkingOrderer(markingsModel, organ, layer));

        UpdateCount();
    }

    private void OnSearchChanged()
    {
        foreach (var element in _searchable)
        {
            element.SetHiddenState(true, SearchBar.Text.Trim());
        }
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();

        _markingsModel.MarkingsReset += UpdateCount;
        _markingsModel.MarkingsChanged += MarkingsChanged;
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();

        _markingsModel.MarkingsReset -= UpdateCount;
        _markingsModel.MarkingsChanged -= MarkingsChanged;
    }

    // There are hundreds of markings and every one of them is a dozen of styled controls, so making them all at once
    // freezes the game. They are added a few at a time while the layer is shown (only a shown control gets frames), the
    // layers that nobody opens don't get them at all.
    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        _pending ??= new Queue<MarkingPrototype>(
            _allMarkings.Values.OrderBy(marking => Loc.GetString($"marking-{marking.ID}")));

        if (_pending.Count == 0)
            return;

        var query = SearchBar.Text.Trim();
        _stopwatch.Restart();

        while (_pending.Count > 0 && _stopwatch.ElapsedMilliseconds < FrameBudgetMs)
        {
            var item = new RfLayerMarkingItem(_markingsModel, _organ, _layer, _pending.Dequeue(), true);
            Items.AddChild(item);
            _searchable.Add(item);

            if (query.Length > 0)
                item.SetHiddenState(true, query);
        }
    }

    private void MarkingsChanged(ProtoId<OrganCategoryPrototype> organ, HumanoidVisualLayers layer)
    {
        if (_organ != organ || _layer != layer)
            return;

        UpdateCount();
    }

    private void UpdateCount()
    {
        _markingsModel.GetMarkingCounts(_organ, _layer, out var isRequired, out var count, out var selected);
        MarkingsStatus.SetPlainText(Loc.GetString(
            "markings-limits",
            ("required", isRequired),
            ("count", count),
            ("selectable", count - selected)));
    }

    protected override void Resized()
    {
        base.Resized();

        Items.Columns = (int) (Width / ColumnWidth);
    }
}
