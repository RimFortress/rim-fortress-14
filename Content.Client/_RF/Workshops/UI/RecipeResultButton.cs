using Content.Client._RF.UserInterface.Controls;

namespace Content.Client._RF.Workshops.UI;

public sealed partial class RecipeResultButton : RfBoxContainer
{
    [Dependency] private IEntityManager _entity = default!;

    public RecipeResultButton()
    {
        IoCManager.InjectDependencies(this);
        InitializeComponent();
    }

    public RecipeResultButton(EntityUid uid) : this()
    {
        if (!_entity.TryGetComponent(uid, out MetaDataComponent? meta)
           || meta.EntityPrototype is not { } proto)
            return;

        Icon.SetPrototype(proto);
        Label.Text = proto.Name;
    }
}
