using Content.Client._RF.UserInterface.Controls;
using Content.Shared._RF.NPC.Systems;
using Content.Shared._RF.Stockpile.Components;
using Content.Shared._RF.Stockpile.Systems;
using Content.Shared._RF.Workshops;
using Content.Shared._RF.Workshops.Prototypes;
using Content.Shared._RF.Workshops.Systems;
using Robust.Shared.Prototypes;

namespace Content.Client._RF.Workshops.UI;

public sealed partial class WorkshopMenu : RfWindow
{
    [Dependency] private IEntityManager _entity = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    private ProtoId<WorkshopRecipeTablePrototype>? _table;
    private ProtoId<WorkshopRecipeGroupPrototype>? _group;
    private HashSet<ProtoId<WorkshopRecipePrototype>> _tableRecipes = new();
    private readonly Dictionary<RecipeQueueButton, int> _queueButtons = new();

    public event Action<int>? OnRemoved;
    public event Action<ProtoId<WorkshopRecipePrototype>>? OnAdded;
    public event Action<int>? OnToggleRepeat;
    public event Action<int>? OnToggleSuspend;
    public event Action<EntityUid?>? OnSupplyStockpile;

    public WorkshopMenu()
    {
        IoCManager.InjectDependencies(this);
        InitializeComponent();

        Search.OnTextChanged += _ => UpdateSearchList();
        ClearSearch.OnPressed += _ => Search.SetText(string.Empty, true);
        BackButton.OnPressed += _ => BackGroup();
        SuppliedStock.OnItemSelected += args =>
        {
            if (args.Value is not EntityUid ent)
                return;

            if (_entity.System<StockpileSystem>().TryGetStock(ent, out var stock))
                OnSupplyStockpile?.Invoke(stock.Value);
            else
                OnSupplyStockpile?.Invoke(null);
        };
    }

    public void SetTitle(EntityUid uid)
    {
        Title = _entity.GetComponent<MetaDataComponent>(uid).EntityPrototype?.Name;
    }

    public void UpdateRecipes(ProtoId<WorkshopRecipeTablePrototype> protoId)
    {
        if (_table == protoId)
            return;

        _table = protoId;
        _tableRecipes = _entity.System<WorkshopSystem>().GetTableRecipes(protoId);

        BuildRecipesList(null);
    }

    public void UpdateContents(NetEntity[] contents)
    {
        ContentsBox.RemoveAllChildren();

        foreach (var uid in contents)
        {
            ContentsBox.AddChild(new RecipeResultButton(_entity.GetEntity(uid)));
        }
    }

    public void UpdateUser(NetEntity? user)
    {
        if (!_entity.TryGetEntity(user, out var uid))
            UserLabel.Text = Loc.GetString("workshop-menu-no-user");
        else
        {
            UserLabel.Text = Loc.GetString("workshop-menu-user",
                ("user", _entity.GetComponent<MetaDataComponent>(uid.Value).EntityName));
        }
    }

    public void UpdateResults(NetEntity[] results, int resultsCapacity)
    {
        ResultsBox.RemoveAllChildren();
        ResultsLabel.Text = $"({results.Length}/{resultsCapacity})";

        foreach (var uid in results)
        {
            ResultsBox.AddChild(new RecipeResultButton(_entity.GetEntity(uid)));
        }
    }

    public void UpdateQueue(WorkshopQueue queue, int maxQueue)
    {
        _queueButtons.Clear();
        QueueBox.RemoveAllChildren();
        QueueLabel.Text = $"({queue.Count}/{maxQueue})";

        if (queue.Count == 0)
            return;

        for (var i = 0; i < queue.Count; i++)
        {
            var entry = queue.Queue[i];
            var path = entry.RemainingPath;

            var button = new RecipeQueueButton(
                entry.Recipe,
                path.Length == 0 ? entry.CraftingEndTime : null,
                repeat: entry.Repeat,
                suspended: entry.Suspended,
                active: path.Length == 0 && i == queue.Index);

            button.OnRemoved += () => OnRemoved?.Invoke(_queueButtons.GetValueOrDefault(button, -1));
            button.OnToggleRepeat += () => OnToggleRepeat?.Invoke(_queueButtons.GetValueOrDefault(button, -1));
            button.OnToggleSuspend += () => OnToggleSuspend?.Invoke(_queueButtons.GetValueOrDefault(button, -1));

            QueueBox.AddChild(button);
            _queueButtons[button] = i;

            for (var j = entry.PathIndex; j < path.Length; j++)
            {
                var subButton = new RecipeQueueButton(
                    path[j],
                    j == entry.PathIndex ? entry.CraftingEndTime : null,
                    subRecipe: true,
                    active: i == queue.Index && j == entry.PathIndex);
                QueueBox.AddChild(subButton);
            }
        }
    }

    public void UpdateSupplying(EntityUid uid)
    {
        SuppliedStock.Clear();
        SuppliedStock.Select(SuppliedStock.AddItem(Loc.GetString("workshop-menu-stock-none")));

        var enumerator = _entity
            .System<OwnershipSystem>()
            .GetEntitiesEnumerator<StockpileComponent>(uid);
        var system = _entity.System<ContainerStockSupplierSystem>();

        while (enumerator.MoveNext(out var stock, out _))
        {
            var item = SuppliedStock.AddItem(_entity.GetComponent<MetaDataComponent>(stock).EntityName, stock);

            if (system.IsSupplying(uid, stock))
                SuppliedStock.Select(item);
        }
    }

    private void BuildRecipesList(ProtoId<WorkshopRecipeGroupPrototype>? group)
    {
        RecipesBox.RemoveAllChildren();
        RecipesScroll.VScroll = 0;
        BackButton.Disabled = group == null;
        _group = group;

        GroupLabel.Text = _proto.TryIndex(group, out var proto) ? proto.Name : string.Empty;

        if (_table == null)
            return;

        var system = _entity.System<WorkshopSystem>();

        foreach (var groupId in system.GetChildrenGroups(_group, _table.Value))
        {
            var button = new WorkshopRecipeButton(groupId, system.GetGroupCount(groupId, _tableRecipes));
            button.Button.OnPressed += _ => BuildRecipesList(groupId);
            RecipesBox.AddChild(button);
        }

        foreach (var recipe in system.GetGroupRecipes(_group, _tableRecipes))
        {
            var button = new WorkshopRecipeButton(recipe, _table.Value);
            button.Button.OnPressed += _ => OnAdded?.Invoke(recipe);
            RecipesBox.AddChild(button);
        }
    }

    private void UpdateSearchList()
    {
        RecipesScroll.VScroll = 0;

        foreach (var child in RecipesBox.Children)
        {
            if (child is not WorkshopRecipeButton button)
                continue;

            button.Visible = button.NameLabel.Text?.Contains(Search.Text) ?? false;
        }
    }

    private void BackGroup()
    {
        if (_table == null)
            return;

        BuildRecipesList(_entity
            .System<WorkshopSystem>()
            .GetParentGroup(_group, _table.Value));
    }
}

