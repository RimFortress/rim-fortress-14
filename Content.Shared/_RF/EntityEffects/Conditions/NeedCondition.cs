using Content.Shared._RF.Needs.Components;
using Content.Shared._RF.Needs.Prototypes;
using Content.Shared._RF.Needs.Systems;
using Content.Shared.EntityConditions;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.EntityEffects.Conditions;

/// <summary>
/// A condition which passes if the specified <see cref="NeedCategoryPrototype"/> is between
/// the specified <see cref="Min"/> and <see cref="Max"/>. If the entity does not have the
/// specified need, the condition evaluates to false.
/// </summary>
public sealed partial class NeedCondition : EntityConditionBase<NeedCondition>
{
    /// <summary>
    /// The value above which this condition will fail. If <see cref="MaxInclusive"/> is false, the condition will fail
    /// if at that value as well.
    /// </summary>
    [DataField]
    public float Max = float.PositiveInfinity;

    /// <summary>
    /// The value below which this condition will fail. If <see cref="MinInclusive"/> is false, the condition will fail
    /// if at that value as well.
    /// </summary>
    [DataField]
    public float Min;

    /// <summary>
    /// If <c>true</c>, values exactly equal to <see cref="Max"/> will NOT fail.
    /// </summary>
    [DataField]
    public bool MaxInclusive;

    /// <summary>
    /// If <c>true</c>, values exactly equal to <see cref="Min"/> will NOT fail.
    /// </summary>
    [DataField]
    public bool MinInclusive;

    /// <summary>
    /// The type of need whose value will be considered.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<NeedCategoryPrototype> Need;

    /// <inheritdoc/>
    public override string EntityConditionGuidebookText(IPrototypeManager prototype)
    {
        return Loc.GetString("entity-condition-guidebook-total-need",
            ("max", float.IsPositiveInfinity(Max) ? int.MaxValue : Max),
            ("min", Min),
            ("type", prototype.Index(Need).Name));
    }
}

public sealed partial class SatiationEntityConditionSystem : EntityConditionSystem<NeedsComponent, NeedCondition>
{
    [Dependency] private NeedsSystem _needs = default!;

    /// <inheritdoc/>
    protected override void Condition(Entity<NeedsComponent> entity, ref EntityConditionEvent<NeedCondition> args)
    {
        if (_needs.TryGetValue(entity.AsNullable(), args.Condition.Need, out var value))
            return;

        args.Result =
            (args.Condition.MinInclusive && value >= args.Condition.Min || value > args.Condition.Min) &&
            (args.Condition.MaxInclusive && value <= args.Condition.Max || value < args.Condition.Max);
    }
}
