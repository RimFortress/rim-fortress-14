using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;

namespace Content.Server._RF.NPC.GOAP.Actions.Basic;

public abstract partial class SetAction<T> : BaseGoapAction<SetAction<T>> where T : notnull
{
    [DataField(required: true)]
    public StateKey<T> Key;

    [DataField(required: true)]
    public T Value;
}

public abstract class SetActionSystem<T1, T2> : GoapActionSystem<T1>
    where T1 : SetAction<T2>
    where T2 : notnull
{
    protected override float ActionCost(Entity<GoapComponent> ent, GoapState state, T1 action) => 0f;

    protected override bool ActionStartup(Entity<GoapComponent> ent, T1 action)
    {
        Set(ent, action.Key, action.Value);
        return true;
    }
}

public sealed partial class SetFloat : SetAction<float>;
public sealed class SetFloatSystem : SetActionSystem<SetFloat, float>;

public sealed partial class SetBool : SetAction<bool>;
public sealed class SetBoolSystem : SetActionSystem<SetBool, bool>;
