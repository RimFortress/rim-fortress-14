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
/// <c>Engagement/RaidSquad/Var/ratio</c>, so a squad knows how strong it is and acts as it may.
/// </summary>
public sealed partial class MeasureSquads : EntityEffectBase<MeasureSquads>
{
    /// <summary>
    /// The kind of the squads.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<EngagementPrototype> Squad;

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
        var threat = _curves.Get(args.Effect.Threat, 0f, ent, _variables.GetVariables(ent.AsNullable()));

        foreach (var owned in _ownership.GetOwned(ent).ToArray())
        {
            if (!_engagementQuery.TryComp(owned, out var comp) || comp.Kind != args.Effect.Squad)
                continue;

            var power = comp.Actors.Values.SelectMany(x => x).Sum(x => _curves.Get(fighter, 0f, x));
            _variables.Set(owned, "power", power);
            _variables.Set(owned, "ratio", power / Math.Max(threat, MinThreat));
        }
    }
}
