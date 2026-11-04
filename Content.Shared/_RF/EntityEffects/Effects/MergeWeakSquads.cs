using System.Linq;
using System.Numerics;
using Content.Shared._RF.MathHelpers.MathCurve;
using Content.Shared._RF.MathHelpers.MathCurve.Systems;
using Content.Shared._RF.MathHelpers.MathVariables;
using Content.Shared._RF.NPC.Engagement;
using Content.Shared._RF.NPC.Engagement.Components;
using Content.Shared._RF.NPC.Engagement.Prototypes;
using Content.Shared._RF.NPC.Engagement.Systems;
using Content.Shared._RF.NPC.Systems;
using Content.Shared.EntityEffects;
using Robust.Shared.Timing;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.EntityEffects.Effects;

/// <summary>
/// Joins the squads that have become too weak (the situations of a kind that the target owns) to the squads that are close to them.
/// Nothing else is changed: the squads that are not weak stay as they are, with their leaders, roles and targets, so a loss in one
/// part of the settlement does not make the whole raid change places. A squad is weak when the curves say so and it has been weak for
/// the time that is set, so a bad moment is not enough. It joins the closest squad, and only if that one is close enough, otherwise it
/// stays where it is, and holds if its stance says so, until somebody comes or the raid loses heart. The members of a squad that joins
/// are given the roles of the other squad by the engagement system. The squads whose participants are all gone are over.
/// </summary>
public sealed partial class MergeWeakSquads : EntityEffectBase<MergeWeakSquads>
{
    /// <summary>
    /// The kind of the squads. They must have the variables of <c>MeasureSquads</c> (<c>power</c> and <c>ratio</c>).
    /// </summary>
    [DataField(required: true)]
    public EntProtoId<EngagementComponent> Squad;

    /// <summary>
    /// Is a squad weak: the curves give more than a half. They see the variables of the target and the <c>power</c> and the
    /// <c>ratio</c> of the squad.
    /// </summary>
    [DataField(required: true)]
    public List<MathCurve> Weak = new();

    /// <summary>
    /// For how long a squad has to be weak, in seconds, to be joined to another.
    /// </summary>
    [DataField]
    public float Delay = 10f;

    /// <summary>
    /// How far from the squad the other can be, for it to join, in tiles.
    /// </summary>
    [DataField]
    public float MaxDistance = 30f;

    /// <summary>
    /// The variable of the target that is 1 while the squads are joined, and 0 otherwise, see <see cref="PlanSquads.PlanningVariable"/>.
    /// </summary>
    [DataField]
    public string PlanningVariable = "planning";
}

public sealed partial class MergeWeakSquadsEntityEffectsSystem : EntityEffectSystem<MathVariablesComponent, MergeWeakSquads>
{
    [Dependency] private EngagementSystem _engagement = null!;
    [Dependency] private OwnershipSystem _ownership = null!;
    [Dependency] private MathCurvesSystem _curves = null!;
    [Dependency] private MathVariablesSystem _variables = null!;
    [Dependency] private SharedTransformSystem _transform = null!;
    [Dependency] private IGameTiming _timing = null!;

    [Dependency] private EntityQuery<EngagementComponent> _engagementQuery;

    private const string WeakSinceVariable = "weakSince";

    protected override void Effect(Entity<MathVariablesComponent> ent, ref EntityEffectEvent<MergeWeakSquads> args)
    {
        var effect = args.Effect;
        var now = (float) _timing.CurTime.TotalSeconds;
        var variables = _variables.GetVariables(ent.AsNullable());
        var squads = new List<Entity<EngagementComponent>>();

        foreach (var owned in _ownership.GetOwned(ent).ToArray())
        {
            if (_engagementQuery.TryComp(owned, out var comp) && Prototype(owned)?.ID == effect.Squad.Id)
                squads.Add((owned, comp));
        }

        // The squads whose participants are all gone are over.
        foreach (var squad in squads.ToArray())
        {
            if (squad.Comp.Actors.Values.Any(x => x.Count > 0))
                continue;

            squads.Remove(squad);
            _engagement.EndEngagement(squad.AsNullable(), EngagementEndReason.Dissolved);
        }

        if (squads.Count < 2)
            return;

        var positions = squads.ToDictionary(x => x.Owner, Center);
        var taken = new HashSet<EntityUid>();

        // The weakest go first.
        foreach (var squad in squads.OrderBy(x => _variables.Get(x.Owner, "power")))
        {
            if (taken.Contains(squad.Owner) || !IsWeak(ent, squad, effect, variables, now))
                continue;

            Entity<EngagementComponent>? closest = null;
            var closestDistance = effect.MaxDistance;

            foreach (var other in squads)
            {
                if (other.Owner == squad.Owner || taken.Contains(other.Owner))
                    continue;

                var distance = Vector2.Distance(positions[squad.Owner], positions[other.Owner]);

                if (distance >= closestDistance)
                    continue;

                closest = other;
                closestDistance = distance;
            }

            if (closest is not { } target)
                continue;

            Join(ent, squad, target, effect);
            taken.Add(squad.Owner);
            taken.Add(target.Owner);
        }
    }

    /// <summary>
    /// Has the squad been weak for long enough.
    /// </summary>
    private bool IsWeak(
        Entity<MathVariablesComponent> ent,
        Entity<EngagementComponent> squad,
        MergeWeakSquads effect,
        Dictionary<string, float> variables,
        float now)
    {
        var squadVariables = new Dictionary<string, float>(variables)
        {
            ["power"] = _variables.Get(squad.Owner, "power"),
            ["ratio"] = _variables.Get(squad.Owner, "ratio"),
        };

        if (_curves.Get(effect.Weak, 0f, ent, squadVariables) <= 0.5f)
        {
            _variables.Set(squad.Owner, WeakSinceVariable, 0f);
            return false;
        }

        var since = _variables.Get(squad.Owner, WeakSinceVariable);

        if (since <= 0f)
        {
            _variables.Set(squad.Owner, WeakSinceVariable, Math.Max(now, 0.001f));
            return false;
        }

        return now - since >= effect.Delay;
    }

    private void Join(
        Entity<MathVariablesComponent> ent,
        Entity<EngagementComponent> from,
        Entity<EngagementComponent> to,
        MergeWeakSquads effect)
    {
        // The leaders who leave are not to be replaced from the squad that is being ended.
        _variables.Set(ent.AsNullable(), effect.PlanningVariable, 1f);

        foreach (var member in from.Comp.Actors.Values.SelectMany(x => x).ToArray())
        {
            if (_engagement.LeaveEngagement(from.AsNullable(), member, EngagementEndReason.Finished))
                _engagement.TryJoinAnyRole(to.AsNullable(), member);
        }

        _engagement.EndEngagement(from.AsNullable(), EngagementEndReason.Dissolved);
        _variables.Set(to.Owner, WeakSinceVariable, 0f);
        _variables.Set(ent.AsNullable(), effect.PlanningVariable, 0f);
    }

    private Vector2 Center(Entity<EngagementComponent> squad)
    {
        var members = squad.Comp.Actors.Values.SelectMany(x => x).ToList();

        return members.Count == 0
            ? Vector2.Zero
            : members.Select(x => _transform.GetWorldPosition(x)).Aggregate(Vector2.Zero, (a, b) => a + b) / members.Count;
    }
}
