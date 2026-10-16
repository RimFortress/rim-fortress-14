using Robust.Client.UserInterface;

namespace Content.Client._RF.UserInterface.Controls.NpcJobs;

public sealed partial class NpcJobsPopCell : RfBoxContainer
{
    [Dependency] private IEntityManager _entity = default!;

    public NpcJobsPopCell()
    {
        InitializeComponent();
    }

    public NpcJobsPopCell(EntityUid uid)
    {
        IoCManager.InjectDependencies(this);
        InitializeComponent();

        Icon.SetEntity(uid);

        if (_entity.TryGetComponent(uid, out MetaDataComponent? comp))
            NameLabel.Text = comp.EntityName;
    }
}

