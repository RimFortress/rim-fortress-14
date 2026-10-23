using Content.Client._RF.UserInterface.Controls;
using Content.Shared._RF.NPC.Components;
using Content.Shared._RF.NPC.Systems;
using Robust.Client.GameObjects;
using Robust.Client.Player;

namespace Content.Client._RF.Info.Controls;

public sealed partial class JobInfo : RfBoxContainer
{
    [Dependency] private IEntityManager _entity = default!;
    [Dependency] private IPlayerManager _player = default!;

    public JobInfo(EntityUid uid, NpcJob data)
    {
        IoCManager.InjectDependencies(this);
        InitializeComponent();

        var npcJobs = System<NpcJobsSystem>();

        if (!_entity.TryGetComponent(_player.LocalEntity, out NpcJobsComponent? comp))
            return;

        if (data.Icon != null)
            Icon.Texture = System<SpriteSystem>().Frame0(data.Icon);

        Label.Text = $"{data.Name}";

        Priority.MaxValue = comp.MaxPriority;
        Priority.Priority = npcJobs.GetPriority(uid, data.Id);
        Priority.OnPriorityChanged += value =>
            npcJobs.SetJobPriority(uid, data.Id, value);
    }
}
