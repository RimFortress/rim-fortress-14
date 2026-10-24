using Robust.Client.UserInterface;
using Content.Client._RF.UserInterface.Controls;

namespace Content.Client._RF.NpcJobs.UI;

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

