using Content.Shared.EntityConditions;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.EntityEffects.Conditions;

/// <summary>
/// A condition which passes if the target is solid: its body collides, so it blocks those who walk, such as a wall.
/// </summary>
public sealed partial class Solid : EntityConditionBase<Solid>
{
    /// <inheritdoc/>
    public override string EntityConditionGuidebookText(IPrototypeManager prototype) => string.Empty;
}

public sealed partial class SolidEntityConditionSystem : EntityConditionSystem<PhysicsComponent, Solid>
{
    protected override void Condition(Entity<PhysicsComponent> ent, ref EntityConditionEvent<Solid> args)
        => args.Result = ent.Comp.CanCollide && ent.Comp.Hard;
}
