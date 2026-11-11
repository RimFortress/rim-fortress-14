using Content.Server.Destructible;
using Content.Shared._RF.EntityEffects.Conditions;
using Content.Shared.EntityConditions;

namespace Content.Server._RF.EntityEffects.Conditions;

public sealed partial class CanBeDestroyedEntityConditionSystem : EntityConditionSystem<DestructibleComponent, CanBeDestroyed>
{
    protected override void Condition(Entity<DestructibleComponent> ent, ref EntityConditionEvent<CanBeDestroyed> args)
        => args.Result = true;
}
