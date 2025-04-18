using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;


public sealed partial class CCVars
{
    /// <summary>
    ///     Maximum number of pops at the roundstart in RimFortress game mode
    /// </summary>
    public static readonly CVarDef<int> MaxRoundstartPops =
        CVarDef.Create("world.max_roundstart_pops", 1, CVar.SERVER | CVar.ARCHIVE | CVar.REPLICATED);
}
