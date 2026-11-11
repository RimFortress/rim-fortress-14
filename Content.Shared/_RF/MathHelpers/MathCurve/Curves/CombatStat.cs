using Content.Shared._RF.Combat;
using Content.Shared._RF.MathHelpers.MathCurve.Systems;
using Content.Shared._RF.NPC.GOAP.Components;

namespace Content.Shared._RF.MathHelpers.MathCurve.Curves;

/// <summary>
/// What a number of the attacks of the user is, see <see cref="CombatStatsSystem"/>.
/// </summary>
public enum CombatStatKind : byte
{
    /// <summary>The damage that a single attack deals, the greatest of them.</summary>
    Blow,

    /// <summary>The damage that a weapon deals in a second, the greatest of them.</summary>
    Dps,

    /// <summary>How far an attack can be made from, the farthest.</summary>
    Range,

    /// <summary>How many attacks can be made until the ammunition is out (infinity if some cannot run out).</summary>
    Shots,
}

/// <summary>
/// A number from what the user can deal in a fight, see <see cref="CombatStatsSystem"/>: the blow, the damage in a second, the reach,
/// the ammunition. Melee and ranged attacks are counted, or only one kind. By default it is what the user holds (the weapon in the hands,
/// the firearm, the hands), which is what it fights with now; with <see cref="Carried"/> it is everything that the user has, which is
/// what it is able to do if it takes the time. 0 if the user has no such attack.
/// </summary>
public sealed partial class CombatStat : BaseMathCurve<CombatStat>
{
    /// <summary>
    /// The number.
    /// </summary>
    [DataField]
    public CombatStatKind Stat = CombatStatKind.Blow;

    /// <summary>
    /// The kind of attacks that are counted, all if it is not set.
    /// </summary>
    [DataField]
    public AttackKind? Kind;

    /// <summary>
    /// Count what the user carries and not only what it holds. The user must be an agent (<c>Goap</c>), otherwise it is only what it holds.
    /// </summary>
    [DataField]
    public bool Carried;
}

public sealed partial class CombatStatCurveSystem : MathCurveSystem<CombatStat>
{
    [Dependency] private CombatStatsSystem _combat = null!;

    [Dependency] private EntityQuery<GoapComponent> _goapQuery;

    private readonly List<CombatAttack> _attacks = new();

    protected override float Curve(CombatStat curve, float input, MathCurveContext ctx)
    {
        if (ctx.User is not { } user)
            return 0f;

        _attacks.Clear();

        if (curve.Carried && _goapQuery.TryComp(user, out var goap))
            _combat.CollectCarried((user, goap), _attacks);
        else
            _combat.CollectHeld(user, _attacks);

        return curve.Stat switch
        {
            CombatStatKind.Blow => CombatStatsSystem.GetBlow(_attacks, curve.Kind),
            CombatStatKind.Dps => CombatStatsSystem.GetDps(_attacks, curve.Kind),
            CombatStatKind.Range => CombatStatsSystem.GetRange(_attacks, curve.Kind),
            CombatStatKind.Shots => CombatStatsSystem.GetShots(_attacks, curve.Kind),
            _ => 0f,
        };
    }
}
