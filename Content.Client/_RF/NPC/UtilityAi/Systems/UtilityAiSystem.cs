using Content.Shared._RF.NPC.UtilityAi.Components;
using Content.Shared._RF.NPC.UtilityAi.Systems;

namespace Content.Client._RF.NPC.UtilityAi.Systems;

public sealed partial class UtilityAiSystem : SharedUtilityAiSystem
{
    public event Action<EntityUid>? OnUtilityAiUpdated;

    [SubscribeLocalEvent]
    private void OnHandle(Entity<UtilityAiComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        OnUtilityAiUpdated?.Invoke(ent);
    }
}
