using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;


public sealed partial class CCVars
{
    /// <summary>
    /// Maximum number of pops at the roundstart in RimFortress game rule
    /// </summary>
    public static readonly CVarDef<int> MaxRoundstartPops =
        CVarDef.Create("world.max_roundstart_pops", 1, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// Maximum number of points a player can spend on expedition equipment in RimFortress game rule
    /// </summary>
    public static readonly CVarDef<int> RoundstartEquipmentPoints =
        CVarDef.Create("world.roundstart_equipment_points", 100, CVar.SERVER | CVar.REPLICATED);
}
