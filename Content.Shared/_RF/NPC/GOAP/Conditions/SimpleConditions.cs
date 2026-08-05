using Content.Shared._RF.NPC.GOAP.Systems;

namespace Content.Shared._RF.NPC.GOAP.Conditions;

public abstract partial class Equals<T> : SimpleGoapCondition<T> where T : IEquatable<T>
{
    public override bool SimpleCheck(GoapState state, IGoapConditionChecker checker)
        => checker.TryGetValue(state, Key, out var value) && value.Equals(Value);
}

public sealed partial class EqualsBool : Equals<bool>;

public sealed partial class EqualsInt : Equals<int>;

public sealed partial class EqualsFloat : Equals<float>;

public sealed partial class EqualsString : Equals<string>;

public abstract partial class NotEquals<T> : SimpleGoapCondition<T> where T : IEquatable<T>
{
    public override bool SimpleCheck(GoapState state, IGoapConditionChecker checker)
        => checker.TryGetValue(state, Key, out var value) && !value.Equals(Value);
}

public sealed partial class NotEqualsBool : NotEquals<bool>;

public sealed partial class NotEqualsInt : NotEquals<int>;

public sealed partial class NotEqualsFloat : NotEquals<float>;

public sealed partial class NotEqualsString : NotEquals<string>;

public sealed partial class MoreThanInt : SimpleGoapCondition<int>
{
    public override bool SimpleCheck(GoapState state, IGoapConditionChecker checker)
        => checker.TryGetValue(state, Key, out var value) && value > Value;
}

public sealed partial class MoreThanFloat : SimpleGoapCondition<float>
{
    public override bool SimpleCheck(GoapState state, IGoapConditionChecker checker)
        => checker.TryGetValue(state, Key, out var value) && value > Value;
}

public sealed partial class MoreThanOrEqualInt : SimpleGoapCondition<int>
{
    public override bool SimpleCheck(GoapState state, IGoapConditionChecker checker)
        => checker.TryGetValue(state, Key, out var value) && value >= Value;
}

public sealed partial class MoreThanOrEqualFloat : SimpleGoapCondition<float>
{
    public override bool SimpleCheck(GoapState state, IGoapConditionChecker checker)
        => checker.TryGetValue(state, Key, out var value) && value >= Value;
}

public sealed partial class LessThanInt : SimpleGoapCondition<int>
{
    public override bool SimpleCheck(GoapState state, IGoapConditionChecker checker)
        => checker.TryGetValue(state, Key, out var value) && value < Value;
}

public sealed partial class LessThanFloat : SimpleGoapCondition<float>
{
    public override bool SimpleCheck(GoapState state, IGoapConditionChecker checker)
        => checker.TryGetValue(state, Key, out var value) && value < Value;
}

public sealed partial class LessThanOrEqualInt : SimpleGoapCondition<int>
{
    public override bool SimpleCheck(GoapState state, IGoapConditionChecker checker)
        => checker.TryGetValue(state, Key, out var value) && value <= Value;
}

public sealed partial class LessThanOrEqualFloat : SimpleGoapCondition<float>
{
    public override bool SimpleCheck(GoapState state, IGoapConditionChecker checker)
        => checker.TryGetValue(state, Key, out var value) && value <= Value;
}

public sealed partial class KeyNotExist : GoapCondition
{
    /// <summary>
    /// The key that this check refers to.
    /// </summary>
    [DataField(required: true)]
    public StateKey<object> Key;

    /// <inheritdoc/>
    public override bool EntityCondition => GoapState.EntityDefaults.Contains(Key);

    public override bool Check(EntityUid target,
        GoapState state,
        IGoapConditionChecker checker,
        out GoapDebugDump? dump)
    {
        dump = null;
        return !checker.TryGetValue(state, Key, out _);
    }
}

public sealed partial class KeyExist : GoapCondition
{
    /// <summary>
    /// The key that this check refers to.
    /// </summary>
    [DataField(required: true)]
    public StateKey<object> Key;

    /// <inheritdoc/>
    public override bool EntityCondition => GoapState.EntityDefaults.Contains(Key);

    public override bool Check(EntityUid target,
        GoapState state,
        IGoapConditionChecker checker,
        out GoapDebugDump? dump)
    {
        dump = null;
        return checker.TryGetValue(state, Key, out _);
    }
}

/// <summary>
/// Checks whether the values of two keys are equal.
/// </summary>
public sealed partial class KeyEqual : GoapCondition
{
    [DataField(required: true)]
    public StateKey<object> Key1;

    [DataField(required: true)]
    public StateKey<object> Key2;

    /// <inheritdoc/>
    public override bool EntityCondition =>
        GoapState.EntityDefaults.Contains(Key1) || GoapState.EntityDefaults.Contains(Key2);

    public override bool Check(EntityUid target,
        GoapState state,
        IGoapConditionChecker checker,
        out GoapDebugDump? dump)
    {
        dump = null;
        return checker.TryGetValue(state, Key1, out var value1)
               && checker.TryGetValue(state, Key2, out var value2)
               && Equals(value1, value2);
    }
}
