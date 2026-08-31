using Content.Shared._RF.NPC.UtilityAi.Components;
using Content.Shared._RF.NPC.UtilityAi.Systems;

namespace Content.Client._RF.NPC.UtilityAi.Systems;

public sealed class UtilityAiSystem : SharedUtilityAiSystem
{
    public event Action<EntityUid>? OnUtilityAiUpdated;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<UtilityAiComponent, AfterAutoHandleStateEvent>(OnHandle);
    }

    private void OnHandle(Entity<UtilityAiComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        OnUtilityAiUpdated?.Invoke(ent);
    }
}
