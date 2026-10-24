using Content.Client._RF.World;
using Content.Shared._RF.Workshops.Prototypes;
using Content.Shared._RF.Workshops.Systems;
using Content.Shared.Localizations;
using Content.Client._RF.UserInterface.Controls;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Input;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._RF.Workshops.UI;

public sealed partial class WorkshopRecipeButton : RfBoxContainer
{
    [Dependency] private IEntityManager _entity = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    private bool _infoBuilt;

    public WorkshopRecipeButton()
    {
        IoCManager.InjectDependencies(this);
        InitializeComponent();
    }

    public WorkshopRecipeButton(
        ProtoId<WorkshopRecipePrototype> protoId,
        ProtoId<WorkshopRecipeTablePrototype> tableId)
    {
        IoCManager.InjectDependencies(this);
        InitializeComponent();

        var proto = _proto.Index(protoId);
        var result = _proto.Index(proto.Result);

        Icon.SetPrototype(proto.Result);
        NameLabel.Text = result.Name;
        TimeLabel.Text = ContentLocalizationManager.FormatTimeShort(
            _entity.System<RimFortressWorldSystem>().ToWorldTime(proto.CraftingTime));
        GroupMarker.Visible = false;

        Button.ToolTip = $"{result.Description}\n{Loc.GetString("workshop-menu-recipe-tooltip")}";

        Button.OnKeyBindUp += args =>
        {
            if (args.Function != EngineKeyFunctions.UseSecondary)
                return;

            BuildInfoBox(protoId, tableId);
            InfoBox.Visible = !InfoBox.Visible;
        };
    }

    public WorkshopRecipeButton(ProtoId<WorkshopRecipeGroupPrototype> protoId, int count)
    {
        IoCManager.InjectDependencies(this);
        InitializeComponent();

        var proto = _proto.Index(protoId);

        Icon.SetPrototype(proto.IconEntity);
        NameLabel.Text = proto.Name;
        TimeLabel.Text = $"{count}";
    }

    private void BuildInfoBox(
        ProtoId<WorkshopRecipePrototype> recipe,
        ProtoId<WorkshopRecipeTablePrototype> tableId)
    {
        if (_infoBuilt)
            return;

        _infoBuilt = true;

        var system = _entity.System<WorkshopSystem>();
        var ingredients = system.GetRecipeIngredients(recipe, tableId);

        ItemsBox.Visible = ingredients.Items.Count > 0;
        MaterialsBox.Visible = ingredients.Materials.Count > 0;
        ReagentsBox.Visible = ingredients.Reagents.Count > 0;

        foreach (var (protoId, count) in ingredients.Items)
        {
            ItemsBox.AddChild(new WorkshopRecipeIngredient(protoId, count));
        }

        foreach (var (protoId, count) in ingredients.Materials)
        {
            MaterialsBox.AddChild(new WorkshopRecipeIngredient(protoId, count));
        }

        foreach (var (protoId, count) in ingredients.Reagents)
        {
            ReagentsBox.AddChild(new WorkshopRecipeIngredient(protoId, count));
        }
    }
}

