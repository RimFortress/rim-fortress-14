using Content.Shared._RF.NPC.GOAP.Systems;

namespace Content.Shared._RF.NPC.GOAP.Conditions;

public abstract partial class Equals<T> : SimpleGoapCondition
    where T : notnull, IEquatable<T>
{
    [DataField(required: true)]
    public T Value = default!;

    public override bool SimpleCheck(GoapState state, IGoapConditionCheсker checker)
        => checker.TryGetValue<T>(state, Key, out var value) && value.Equals(Value);
}

public sealed partial class EqualsBool : Equals<bool>;

public sealed partial class EqualsInt : Equals<int>;

public sealed partial class EqualsFloat : Equals<float>;

public abstract partial class NotEquals<T> : SimpleGoapCondition
    where T : notnull, IEquatable<T>
{
    [DataField(required: true)]
    public T Value = default!;

    public override bool SimpleCheck(GoapState state, IGoapConditionCheсker checker)
        => checker.TryGetValue<T>(state, Key, out var value) && !value.Equals(Value);
}

public sealed partial class NotEqualsBool : NotEquals<bool>;

public sealed partial class NotEqualsInt : NotEquals<int>;

public sealed partial class NotEqualsFloat : NotEquals<float>;

public sealed partial class MoreThanInt : SimpleGoapCondition
{
    [DataField(required: true)]
    public int Value = default!;

    public override bool SimpleCheck(GoapState state, IGoapConditionCheсker checker)
        => checker.TryGetValue<int>(state, Key, out var value) && value > Value;
}

public sealed partial class MoreThanFloat : SimpleGoapCondition
{
    [DataField(required: true)]
    public float Value = default!;

    public override bool SimpleCheck(GoapState state, IGoapConditionCheсker checker)
        => checker.TryGetValue<float>(state, Key, out var value) && value > Value;
}

public sealed partial class MoreThanOrEqualInt : SimpleGoapCondition
{
    [DataField(required: true)]
    public int Value = default!;

    public override bool SimpleCheck(GoapState state, IGoapConditionCheсker checker)
        => checker.TryGetValue<int>(state, Key, out var value) && value >= Value;
}

public sealed partial class MoreThanOrEqualFloat : SimpleGoapCondition
{
    [DataField(required: true)]
    public float Value = default!;

    public override bool SimpleCheck(GoapState state, IGoapConditionCheсker checker)
        => checker.TryGetValue<float>(state, Key, out var value) && value >= Value;
}

public sealed partial class LessThanInt : SimpleGoapCondition
{
    [DataField(required: true)]
    public int Value = default!;

    public override bool SimpleCheck(GoapState state, IGoapConditionCheсker checker)
        => checker.TryGetValue<int>(state, Key, out var value) && value < Value;
}

public sealed partial class LessThanFloat : SimpleGoapCondition
{
    [DataField(required: true)]
    public float Value = default!;

    public override bool SimpleCheck(GoapState state, IGoapConditionCheсker checker)
        => checker.TryGetValue<float>(state, Key, out var value) && value < Value;
}

public sealed partial class LessThanOrEqualInt : SimpleGoapCondition
{
    [DataField(required: true)]
    public int Value = default!;

    public override bool SimpleCheck(GoapState state, IGoapConditionCheсker checker)
        => checker.TryGetValue<int>(state, Key, out var value) && value <= Value;
}

public sealed partial class LessThanOrEqualFloat : SimpleGoapCondition
{
    [DataField(required: true)]
    public float Value = default!;

    public override bool SimpleCheck(GoapState state, IGoapConditionCheсker checker)
        => checker.TryGetValue<float>(state, Key, out var value) && value <= Value;
}

public sealed partial class KeyNotExist : SimpleGoapCondition
{
    public override bool SimpleCheck(GoapState state, IGoapConditionCheсker checker)
        => !state.ContainsKey(Key);
}

public sealed partial class KeyExist : SimpleGoapCondition
{
    public override bool SimpleCheck(GoapState state, IGoapConditionCheсker checker)
        => state.ContainsKey(Key);
}
