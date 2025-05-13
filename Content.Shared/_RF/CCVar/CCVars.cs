using Robust.Shared;
using Robust.Shared.Configuration;

namespace Content.Shared._RF.CCVar;

[CVarDefs]
public sealed class RfVars : CVars
{
    /// <summary>
    /// Maximum number of pops at the roundstart in RimFortress game rule
    /// </summary>
    public static readonly CVarDef<int> MaxRoundstartPops =
        CVarDef.Create("rimfortress.max_roundstart_pops", 1, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// Maximum number of points a player can spend on expedition equipment in RimFortress game rule
    /// </summary>
    public static readonly CVarDef<int> RoundstartEquipmentPoints =
        CVarDef.Create("rimfortress.roundstart_equipment_points", 100, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// Pops outside this radius will be considered part of different player settlements.
    /// Used to find the coordinates of player settlements
    /// </summary>
    public static readonly CVarDef<int> MaxSettlementRadius =
        CVarDef.Create("rimfortress.max_settlement_radius", 100, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// The minimum size of a group of pops necessary to be considered a settlement
    /// </summary>
    public static readonly CVarDef<int> MinSettlementMembers =
        CVarDef.Create("rimfortress.min_settlement_members", 2, CVar.SERVER | CVar.REPLICATED);
}
