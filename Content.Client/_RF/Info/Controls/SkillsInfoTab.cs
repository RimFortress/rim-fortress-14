using Content.Shared._RF.Skills.Components;

namespace Content.Client._RF.Info.Controls;

public sealed partial class SkillsInfoTab : InfoTab
{
    [Dependency] private IEntityManager _entity = default!;

    // Every skill refreshes itself.
    public SkillsInfoTab() : base(0f)
    {
        IoCManager.InjectDependencies(this);
        InitializeComponent();
    }

    protected override void Update(EntityUid uid)
    {
        Container.RemoveAllChildren();

        if (!_entity.TryGetComponent(uid, out SkillsComponent? skills))
            return;

        foreach (var data in skills.Skills)
        {
            Container.AddChild(new SkillInfo(uid, data.Id));
        }
    }
}
