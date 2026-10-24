using Content.Client.Construction;
using Content.Shared.Construction.Prototypes;
using Content.Client._RF.UserInterface.Controls;
using Robust.Client.Placement;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client._RF.Construction.Controls;

public sealed partial class ConstructionButton : RfButton
{
    [Dependency] private IPlacementManager _placement = default!;
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private ILocalizationManager _locale = default!;
    [Dependency] private IPrototypeManager _prototype = default!;

    public ConstructionButton(ConstructionPrototype prototype)
    {
        IoCManager.InjectDependencies(this);
        InitializeComponent();

        var construction = _entityManager.System<ConstructionSystem>();

        NameLabel.Text = prototype.SetName.HasValue ? Loc.GetString(prototype.SetName) : prototype.Name;

        if (construction.TryGetRecipePrototype(prototype.ID, out var recipe)
            && _prototype.TryIndex(recipe, out var target))
            Texture.SetPrototype(target);

        OnPressed += _ =>
        {
            if (_placement.IsActive)
                _placement.Clear();

            _placement.BeginPlacing(
                new PlacementInformation { IsTile = false, PlacementOption = prototype.PlacementMode },
                new CommonConstructionPlacementHijack(_entityManager.System<CommonConstructionSystem>(), prototype));
        };
    }

    public ConstructionButton(string name, EntityPrototype proto)
    {
        IoCManager.InjectDependencies(this);
        InitializeComponent();

        NameLabel.Text = _locale.TryGetString(name, out var locName) ? locName : name;
        Texture.SetPrototype(proto);
    }
}

