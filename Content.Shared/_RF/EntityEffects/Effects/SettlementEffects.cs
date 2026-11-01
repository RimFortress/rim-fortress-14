using Content.Shared._RF.NPC.GOAP;
using Content.Shared.EntityEffects;
using Robust.Shared.Map;

namespace Content.Shared._RF.EntityEffects.Effects;

/// <summary>
/// Puts the pops that live in the settlement that the target world rule is about, as they are at this moment, into a key of the
/// state of the target. It is a copy: those who come to the settlement later are not in it, and those who leave it are, which is
/// what the search queries of the rule (<c>KeyEntities</c>) need to tell how many of them have been killed.
/// </summary>
public sealed partial class CopySettlementResidents : EntityEffectBase<CopySettlementResidents>
{
    /// <summary>
    /// The key.
    /// </summary>
    [DataField]
    public StateKey<HashSet<EntityUid>> Key = "Targets";
}

/// <summary>
/// Puts the coordinates of the center of the settlement that the target world rule is about into a key of its own state. The
/// participants of the situations that it owns read it from there, see <c>Engagement/Raid/Owner/SettlementTarget</c>.
/// </summary>
public sealed partial class SetSettlementTarget : EntityEffectBase<SetSettlementTarget>
{
    /// <summary>
    /// The key.
    /// </summary>
    [DataField]
    public StateKey<EntityCoordinates> Key = "SettlementTarget";
}
