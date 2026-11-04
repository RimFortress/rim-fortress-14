using System.Linq;
using Content.Shared._RF.MathHelpers.MathCurve;
using Content.Shared._RF.MathHelpers.MathCurve.Systems;
using Content.Shared._RF.MathHelpers.MathVariables;
using Content.Shared._RF.NPC.Engagement.Components;
using Content.Shared._RF.NPC.Engagement.Prototypes;
using Content.Shared._RF.NPC.Systems;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.EntityEffects.Effects;

/// <summary>
/// Measures the squads (the situations of a kind that the target owns): how strong each is, and how strong it is against what the
/// squads are up against. They are the variables <c>power</c> and <c>ratio</c> of the squad, which its participants read as
/// <c>Engagement/RaidSquad/Var/ratio</c>, so a squad knows how strong it is and acts as it may. What it may is the stance of
/// the squad, a number that <see cref="Stance"/> makes of the power and the ratio (and the variables of the target), such as
/// hold, keep together, rush. The participants decide what to do by it, the goals of the utility AI look at the variable.
/// </summary>
public sealed partial class MeasureSquads : EntityEffectBase<MeasureSquads>
{
    /// <summary>
    /// The kind of the squads.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId<EngagementComponent> Squad;

    /// <summary>
    /// How strong what the squads are up against is. The curves see the variables of the target.
    /// </summary>
    [DataField(required: true)]
    public List<MathCurve> Threat = new();

    /// <summary>
    /// How strong a fighter is, calculated with the fighter as the user.
    /// </summary>
    [DataField(required: true)]
    public List<MathCurve> Power = new();

    /// <summary>
    /// The stance of a squad, written to its variable <c>stance</c>. The curves see the variables of the target, and the
    /// <c>power</c> and the <c>ratio</c> of the squad. The stance is not measured if it is not set.
    /// </summary>
    [DataField]
    public List<MathCurve> Stance = new();
}

public sealed partial class MeasureSquadsEntityEffectsSystem : EntityEffectSystem<MathVariablesComponent, MeasureSquads>
{
    [Dependency] private OwnershipSystem _ownership = null!;
    [Dependency] private MathCurvesSystem _curves = null!;
    [Dependency] private MathVariablesSystem _variables = null!;

    [Dependency] private EntityQuery<EngagementComponent> _engagementQuery;

    /// <summary>
    /// The threat that is counted as nothing, so the ratio is a number.
    /// </summary>
    private const float MinThreat = 0.01f;

    protected override void Effect(Entity<MathVariablesComponent> ent, ref EntityEffectEvent<MeasureSquads> args)
    {
        var fighter = args.Effect.Power;
        var variables = _variables.GetVariables(ent.AsNullable());
        var threat = _curves.Get(args.Effect.Threat, 0f, ent, variables);

        foreach (var owned in _ownership.GetOwned(ent).ToArray())
        {
            if (!_engagementQuery.TryComp(owned, out var comp) || Prototype(owned)?.ID != args.Effect.Squad.Id)
                continue;

            var power = comp.Actors.Values.SelectMany(x => x).Sum(x => _curves.Get(fighter, 0f, x));
            _variables.Set(owned, "power", power);
            var ratio = power / Math.Max(threat, MinThreat);
            _variables.Set(owned, "ratio", ratio);

            if (args.Effect.Stance.Count == 0)
                continue;

            var squadVariables = new Dictionary<string, float>(variables) { ["power"] = power, ["ratio"] = ratio };
            var stance = _curves.Get(args.Effect.Stance, 0f, ent, squadVariables);
            _variables.Set(owned, "stance", stance);
        }
    }
}
