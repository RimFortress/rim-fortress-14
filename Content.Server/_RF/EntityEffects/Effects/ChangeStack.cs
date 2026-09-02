using Content.Server.Stack;
using Content.Shared.EntityEffects;
using Content.Shared.Stacks;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.EntityEffects.Effects;

/// <summary>
/// Changes the amount of material in the stack
/// </summary>
public sealed partial class ChangeStack : EntityEffectBase<ChangeStack>
{
    /// <summary>
    /// Amount of material to be changed to
    /// </summary>
    [DataField]
    public int Amount;
}

public sealed partial class ChangeStackEntityEffectSystem : EntityEffectSystem<StackComponent, ChangeStack>
{
    [Dependency] private StackSystem _stack = default!;

    protected override void Effect(Entity<StackComponent> entity, ref EntityEffectEvent<ChangeStack> args)
    {
        _stack.SetCount(entity.AsNullable(), _stack.GetCount(entity.AsNullable()) + args.Effect.Amount);
    }
}
