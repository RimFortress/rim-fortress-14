using Content.Shared._RF.World;
using Content.Shared._RF.World.Components;

namespace Content.Client._RF.World;

public sealed partial class RimFortressWorldSystem : SharedRimFortressWorldSystem
{
    public event Action<Entity<RimFortressPlayerComponent>>? OnPlayerUpdate;

    [SubscribeLocalEvent]
    private void OnPlayerHandleState(Entity<RimFortressPlayerComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        OnPlayerUpdate?.Invoke(ent);
    }
}
