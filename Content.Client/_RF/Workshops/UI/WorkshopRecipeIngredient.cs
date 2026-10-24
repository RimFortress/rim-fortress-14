using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Stacks;
using Content.Client._RF.UserInterface.Controls;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Client._RF.Workshops.UI;

public sealed partial class WorkshopRecipeIngredient : RfBoxContainer
{
    [Dependency] private IEntityManager _entity = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    private const string ReagentIconPath = "/Textures/_RF/Interface/droplet-solid-full.svg.192dpi.png";

    public WorkshopRecipeIngredient()
    {
        IoCManager.InjectDependencies(this);
        InitializeComponent();
    }

    public WorkshopRecipeIngredient(EntProtoId protoId, int count)
    {
        IoCManager.InjectDependencies(this);
        InitializeComponent();
        ProtoIcon.SetPrototype(protoId);
        Label.Text = _proto.Index(protoId).Name;
        Quantity.Text = $"x{count}";
    }

    public WorkshopRecipeIngredient(ProtoId<StackPrototype> protoId, int count)
    {
        IoCManager.InjectDependencies(this);
        InitializeComponent();
        var system = _entity.System<SpriteSystem>();
        var proto = _proto.Index(protoId);
        Icon.Texture = proto.Icon != null ? system.Frame0(proto.Icon) : null;
        Label.Text = Loc.GetString(proto.Name);
        Quantity.Text = $"x{count}";
    }

    public WorkshopRecipeIngredient(ProtoId<ReagentPrototype> protoId, FixedPoint2 quantity)
    {
        IoCManager.InjectDependencies(this);
        InitializeComponent();
        var proto = _proto.Index(protoId);
        ProtoIcon.Visible = false;
        Icon.Visible = true;
        Icon.TexturePath = ReagentIconPath;
        Icon.ModulateSelfOverride = proto.SubstanceColor;
        Label.Text = Loc.GetString(proto.LocalizedName);
        Quantity.Text = Loc.GetString("workshop-menu-reagent-quantity", ("quantity", quantity));
    }

    protected override void Parented(Control newParent)
    {
        base.Parented(newParent);

        var index = 0;

        foreach (var child in newParent.Children)
        {
            if (child is not WorkshopRecipeIngredient control)
                continue;

            index++;

            if (control != this)
                continue;

            Number.Text = $"{index}.";
            break;
        }
    }
}

