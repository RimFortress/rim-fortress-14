using System.Linq;
using System.Numerics;
using Content.Shared._RF.MathHelpers.MathCurve;
using Content.Shared._RF.MathHelpers.MathCurve.Systems;
using Content.Shared._RF.MathHelpers.MathVariables;
using Content.Shared._RF.NPC.Engagement.Components;
using Content.Shared._RF.NPC.Engagement.Prototypes;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;
using Content.Shared._RF.NPC.Systems;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.EntityEffects.Effects;

/// <summary>
/// Shares the targets between the squads that the target owns: every target is given to one squad, and every squad has its share
/// in a key of the state of the squad, which its participants read as <c>Engagement/RaidSquad/State/Objective</c>. A target goes to
/// the squad it costs the least, as the cost curves say: that can be how far it is from the squad, and how much of the targets
/// the squad has already got compared with its strength (<c>load</c> and <c>capacity</c>). The strongest targets are shared first.
/// A target that a squad has stays with it: only those that nobody has are shared, so it can be applied as often as the squads or
/// the targets may have changed, and nothing is passed over.
/// </summary>
public sealed partial class AssignSquadTargets : EntityEffectBase<AssignSquadTargets>
{
    /// <summary>
    /// The kind of the squads. They must have a state (<c>Goap</c>) and the variable <c>power</c>, see <c>MeasureSquads</c>.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId<EngagementComponent> Squad;

    /// <summary>
    /// The key of the state of the target with the targets, those that are to be shared.
    /// </summary>
    [DataField(required: true)]
    public StateKey<IReadOnlyCollection<EntityUid>> Targets;

    /// <summary>
    /// The key of the state of a squad that its share is put into.
    /// </summary>
    [DataField]
    public StateKey<HashSet<EntityUid>> Key = "Objective";

    /// <summary>
    /// How strong a target is, calculated with the target as the user. It is the weight of the target in the share.
    /// </summary>
    [DataField(required: true)]
    public List<MathCurve> Power = new();

    /// <summary>
    /// What it costs a squad to take a target, calculated with the target as the user. The least cost wins. The curves see the
    /// variables of the target of the effect and: <c>distance</c> from the squad to the target, <c>load</c> the power of the
    /// targets the squad has got already, <c>capacity</c> the power of the squad, <c>power</c> of the target.
    /// </summary>
    [DataField(required: true)]
    public List<MathCurve> Cost = new();
}

public sealed partial class AssignSquadTargetsEntityEffectsSystem : EntityEffectSystem<GoapComponent, AssignSquadTargets>
{
    [Dependency] private SharedGoapSystem _goap = null!;
    [Dependency] private OwnershipSystem _ownership = null!;
    [Dependency] private MathCurvesSystem _curves = null!;
    [Dependency] private MathVariablesSystem _variables = null!;
    [Dependency] private SharedTransformSystem _transform = null!;

    [Dependency] private EntityQuery<EngagementComponent> _engagementQuery;
    [Dependency] private EntityQuery<GoapComponent> _goapQuery;

    /// <summary>
    /// The power that a squad has, so the load is a number.
    /// </summary>
    private const float MinCapacity = 0.01f;

    private sealed class SquadState(Entity<EngagementComponent> squad, GoapComponent goap, Vector2 position, float capacity)
    {
        public readonly Entity<EngagementComponent> Squad = squad;
        public readonly GoapComponent Goap = goap;
        public readonly Vector2 Position = position;
        public readonly float Capacity = capacity;
        public readonly HashSet<EntityUid> Share = new();
        public float Load;
    }

    protected override void Effect(Entity<GoapComponent> ent, ref EntityEffectEvent<AssignSquadTargets> args)
    {
        var effect = args.Effect;
        var squads = new List<SquadState>();

        foreach (var owned in _ownership.GetOwned(ent).ToArray())
        {
            if (!_engagementQuery.TryComp(owned, out var comp)
                || Prototype(owned)?.ID != effect.Squad.Id
                || !_goapQuery.TryComp(owned, out var goap))
                continue;

            var members = comp.Actors.Values.SelectMany(x => x).ToList();

            if (members.Count == 0)
                continue;

            var position = members
                .Select(x => _transform.GetWorldPosition(x))
                .Aggregate(Vector2.Zero, (a, b) => a + b) / members.Count;

            squads.Add(new SquadState((owned, comp),
                goap,
                position,
                Math.Max(MinCapacity, _variables.Get(owned, "power"))));
        }

        if (squads.Count == 0)
            return;

        _goap.TryGetValue(ent.Comp.State, effect.Targets, out var targets);
        var variables = _variables.GetVariables(ent.Owner);
        var weighted = (targets ?? Array.Empty<EntityUid>())
            .Where(x => !TerminatingOrDeleted(x))
            .Select(x => (Uid: x, Power: _curves.Get(effect.Power, 0f, x)))
            .OrderByDescending(x => x.Power)
            .ToList();

        // Who has a target keeps it: the targets are not passed from squad to squad, and the squads do not run across the settlement
        // because of a loss somewhere. Only the targets that nobody has are given out: the ones that were the share of a squad that is
        // gone, and the new ones.
        var free = new List<(EntityUid Uid, float Power)>();

        foreach (var (target, power) in weighted)
        {
            var owner = squads.FirstOrDefault(x =>
                SharedGoapSystem.TryGetValueNoEcsDefaults(x.Goap.State, effect.Key, out var share) && share.Contains(target));

            if (owner == null)
            {
                free.Add((target, power));
                continue;
            }

            owner.Share.Add(target);
            owner.Load += power;
        }

        foreach (var (target, power) in free)
        {
            var targetPosition = _transform.GetWorldPosition(target);
            SquadState? best = null;
            var bestCost = float.MaxValue;

            foreach (var squad in squads)
            {
                var costVariables = new Dictionary<string, float>(variables)
                {
                    ["distance"] = Vector2.Distance(squad.Position, targetPosition),
                    ["load"] = squad.Load,
                    ["capacity"] = squad.Capacity,
                    ["power"] = power,
                };

                var cost = _curves.Get(effect.Cost, 0f, target, costVariables);

                if (cost >= bestCost)
                    continue;

                best = squad;
                bestCost = cost;
            }

            if (best == null)
                continue;

            best.Share.Add(target);
            best.Load += power;
        }

        foreach (var squad in squads)
        {
            // Only what has changed is put in: the queries of the participants follow the key.
            if (SharedGoapSystem.TryGetValueNoEcsDefaults(squad.Goap.State, effect.Key, out var share) && share.SetEquals(squad.Share))
                continue;

            _goap.SetValue(squad.Goap.State, effect.Key, squad.Share);
        }
    }
}
