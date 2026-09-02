using Content.Shared._RF.MathHelpers.MathCurve.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Nutrition.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.MathHelpers.MathCurve.Curves;

/// <summary>
/// Returns the entity's saturation level.
/// </summary>
public sealed partial class Satiation : BaseMathCurve<Satiation>
{
    /// <summary>
    /// Saturation type.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<SatiationTypePrototype> Type;

    /// <summary>
    /// Will the value be normalized relative to the maximum saturation value?
    /// </summary>
    [DataField]
    public bool Normalize = true;
}

public sealed partial class HungerLevelSystem : MathCurveSystem<Satiation>
{
    [Dependency] private SatiationSystem _satiation = default!;

    protected override float Curve(Satiation curve, float input, MathCurveContext ctx)
    {
        if (!TryComp(ctx.User, out SatiationComponent? satiation))
            return 0f;

        var ent = new Entity<SatiationComponent>(ctx.User.Value, satiation);
        var value = _satiation.GetValueOrNull(ent, curve.Type) ?? 0f;

        if (!curve.Normalize || value == 0f)
            return value;

        var max = _satiation.GetMaximumValue(ent, curve.Type);

        if (max == null)
            return 1 - 1 / (value + 2f);

        return value / max.Value;
    }
}
