namespace Content.Server._RF.NPC.Queries.Curves;

/// <summary>
/// Normalizes the input value
/// </summary>
public sealed partial class NormCurve : RfUtilityCurve
{
    public override float Curve(float value) => 1 - 1 / value;
}
