using Content.Shared._RF.MathHelpers.MathCurve;
using Content.Shared._RF.MathHelpers.MathCurve.Systems;
using Content.Shared._RF.MathHelpers.MathVariables;
using Content.Shared.EntityEffects;

namespace Content.Shared._RF.EntityEffects.Effects;

/// <summary>
/// Sets a variable of the entity to what the curves give.
/// </summary>
public sealed partial class SetMathVariable : EntityEffectBase<SetMathVariable>
{
    /// <summary>
    /// The name of the variable.
    /// </summary>
    [DataField(required: true)]
    public string Key = string.Empty;

    /// <summary>
    /// What it is set to. The curves see the variables of the entity.
    /// The current value of the variable being modified is passed as input,
    /// or zero if the variable is not specified.
    /// </summary>
    [DataField(required: true)]
    public List<MathCurve> Value = new();
}

public sealed partial class SetMathVariableEntityEffectsSystem : EntityEffectSystem<MathVariablesComponent, SetMathVariable>
{
    [Dependency] private MathVariablesSystem _variables = null!;
    [Dependency] private MathCurvesSystem _curves = null!;

    protected override void Effect(Entity<MathVariablesComponent> ent, ref EntityEffectEvent<SetMathVariable> args)
    {
        var current = _variables.Get(ent.AsNullable(), args.Effect.Key);
        var vars = _variables.GetVariables(ent.AsNullable());
        var value = _curves.Get(args.Effect.Value, current, ent, vars);
        _variables.Set(ent.AsNullable(), args.Effect.Key, value);
    }
}
