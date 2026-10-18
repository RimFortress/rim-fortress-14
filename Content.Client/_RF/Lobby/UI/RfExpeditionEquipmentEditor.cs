using Content.Suil;
using Content.Client._RF.UserInterface;
using System.Linq;
using Content.Client._RF.UserInterface.Controls;
using Content.Shared._RF.Preferences;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Client._RF.Lobby.UI;

public sealed partial class RfExpeditionEquipmentEditor : RfBoxContainer
{
    [Dependency] private IPrototypeManager _protoManager = default!;
    [Dependency] private IPlayerEquipmentManager _equipment = default!;

    private readonly RfLobbyUIController _lobby;

    private int _leftPoints;
    private ExpeditionEquipmentPrototype? _selectedCategory;
    private Dictionary<EntProtoId, int> _itemsSelected = new();
    private readonly Dictionary<EntProtoId, int> _costs = new();
    private readonly Dictionary<EntProtoId, ExpeditionItemButton> _buttons = new();

    public bool IsDirty
    {
        get;
        private set
        {
            field = value;

            SaveButton.Disabled = !value;
            ResetButton.Disabled = !value;
        }
    }

    public RfExpeditionEquipmentEditor()
    {
        IoCManager.InjectDependencies(this);
        InitializeComponent();

        _lobby = UserInterfaceManager.GetUIController<RfLobbyUIController>();

        ResetButton.OnPressed += _ => BuildList();
        SaveButton.OnPressed += _ => SaveSettings();
        Search.OnTextChanged += _ => BuildItems();
        ClearSearch.OnPressed += _ => Search.SetText(string.Empty, true);
    }

    public void SaveSettings()
    {
        _equipment.SetPlayerEquipment(_itemsSelected);
        IsDirty = false;
        BuildList();
    }

    public void BuildList()
    {
        _costs.Clear();
        _itemsSelected = new(_equipment.Equipment);
        IsDirty = false;
        CategoryBox.RemoveAllChildren();

        var prototypes = _protoManager.EnumeratePrototypes<ExpeditionEquipmentPrototype>().ToList();
        var categoryGroup = new RfButtonGroup();

        if (_selectedCategory != null && !prototypes.Contains(_selectedCategory))
            _selectedCategory = null;

        foreach (var proto in prototypes)
        {
            if (proto.Hidden)
                continue;

            foreach (var (entProto, cost) in proto.Items)
            {
                _costs.Add(entProto, cost);
            }

            var categoryButton = new RfButton
            {
                Group = categoryGroup,
                Text = CategoryPoints(proto) is var points && points != 0
                    ? $"{proto.Name} ({CategoryPoints(proto)})"
                    : proto.Name,
            };

            categoryButton.OnPressed += _ =>
            {
                _selectedCategory = proto;
                BuildItems();
            };

            categoryButton.AddSuilStyleClass(SuilClass.FontBig);
            CategoryBox.AddChild(categoryButton);
        }

        BuildItems();
        Recount();
    }

    private void BuildItems()
    {
        ItemBox.RemoveAllChildren();
        _buttons.Clear();

        if (_selectedCategory == null)
            return;

        foreach (var (proto, cost) in _selectedCategory.Items)
        {
            var count = _itemsSelected.GetValueOrDefault(proto, 0);
            var itemButton = new ExpeditionItemButton(proto, cost, count);

            if (!string.IsNullOrEmpty(Search.Text)
                && !itemButton.ItemName.Contains(Search.Text))
                continue;

            itemButton.Counter.DisableIncrease = cost > _leftPoints;
            itemButton.OnPointsSet += args =>
            {
                if (_leftPoints + args.Old - args.New < 0)
                    itemButton.Count = (_leftPoints + args.Old) / itemButton.Cost;

                _itemsSelected[proto] = itemButton.Count;
                Recount();
                SetDirty();
            };

            _buttons.Add(proto, itemButton);
            ItemBox.AddChild(itemButton);
        }
    }

    private void Recount()
    {
        _leftPoints = _lobby.LeftPoints();

        foreach (var (proto, button) in _buttons)
        {
            if (!_costs.TryGetValue(proto, out var cost))
                continue;

            button.Counter.DisableIncrease = cost > _leftPoints;
        }

        PointsLabel.Text =
            $"{Loc.GetString("expedition-equipment-editor-points-left")}: {_leftPoints}";
    }

    private void SetDirty()
    {
        foreach (var (proto, count) in _itemsSelected)
        {
            if (_equipment.Equipment.TryGetValue(proto, out var value) && value == count)
                continue;

            if (count == 0 && !_equipment.Equipment.ContainsKey(proto))
                continue;

            IsDirty = true;
            return;
        }

        IsDirty = false;
    }

    public int UsedPoints()
    {
        var points = 0;

        foreach (var (proto, count) in _itemsSelected)
        {
            if (!_costs.TryGetValue(proto, out var cost))
                continue;

            points += count * cost;
        }

        return points;
    }

    private int CategoryPoints(ExpeditionEquipmentPrototype prototype)
    {
        var points = 0;

        foreach (var (proto, cost) in prototype.Items)
        {
            if (_itemsSelected.TryGetValue(proto, out var count))
                points += count * cost;
        }

        return points;
    }
}

