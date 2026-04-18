using Content.Shared._RF.NPC.GOAP.Components;

namespace Content.Shared._RF.NPC.GOAP.Systems;

/// <summary>
/// An entity system that implements GOAP condition check.
/// </summary>
/// <typeparam name="T">GOAP condtition type.</typeparam>
public abstract class GoapConditionSystem<T> : EntitySystem where T : BaseGoapCondition<T>
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GoapComponent, GoapConditionCheck<T>>(OnConditionCheck);
    }

    protected abstract void OnConditionCheck(Entity<GoapComponent> ent, ref GoapConditionCheck<T> args);
}
