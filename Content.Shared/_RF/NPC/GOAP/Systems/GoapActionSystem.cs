using Content.Shared._RF.NPC.GOAP.Components;

namespace Content.Shared._RF.NPC.GOAP.Systems;

/// <summary>
/// An entity system that provides GOAP action functionality.
/// </summary>
/// <typeparam name="T">GOAP action type.</typeparam>
public abstract class GoapActionSystem<T> : EntitySystem where T : GoapAction
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GoapComponent, GoapActionUpdate<T>>(OnActionUpdate);
    }

    protected abstract void OnActionUpdate(Entity<GoapComponent> ent, ref GoapActionUpdate<T> args);
}
