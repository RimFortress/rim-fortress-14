using Content.Server.NPC.Queries.Curves;

namespace Content.Server._RF.NPC.Queries;

public abstract partial class RfUtilityCurve : IUtilityCurve
{
    public abstract float Curve(float value);
}
