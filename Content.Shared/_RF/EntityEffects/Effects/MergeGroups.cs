using System.Linq;
using System.Numerics;
using Content.Shared._RF.MathHelpers.MathCurve;
using Content.Shared._RF.MathHelpers.MathCurve.Systems;
using Content.Shared._RF.MathHelpers.MathVariables;
using Content.Shared._RF.NPC.Engagement.Components;
using Content.Shared._RF.NPC.Groups.Systems;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._RF.EntityEffects.Effects;

/// <summary>
/// Joins the groups that have become too weak (the situations of a kind that the target owns) to the groups that are close to them,
/// see <see cref="GroupsSystem.Merge"/>. Nothing else is changed: the groups that are not weak stay as they are, with their leaders,
/// roles and targets, so a loss in one part of the settlement does not make the whole force change places. A group is weak when the
/// curves say so and it has been weak for the time that is set, so a bad moment is not enough. It joins the closest group, and
/// only if that one is close enough, otherwise it stays where it is until somebody comes. The members of a group that joins are
/// given the roles of the other group by the engagement system. The groups whose participants are all gone are over.
/// </summary>
public sealed partial class MergeGroups : EntityEffectBase<MergeGroups>
{
    /// <summary>
    /// The kind of the groups. They must have the variables of <see cref="MeasureGroups"/> (<c>power</c> and <c>ratio</c>).
    /// </summary>
    [DataField(required: true)]
    public EntProtoId<EngagementComponent> Group;

    /// <summary>
    /// Is a group weak: the curves give more than a half. They see the variables of the target and the <c>power</c> and the
    /// <c>ratio</c> of the group.
    /// </summary>
    [DataField(required: true)]
    public List<MathCurve> Weak = new();

    /// <summary>
    /// For how long a group has to be weak, in seconds, to be joined to another.
    /// </summary>
    [DataField]
    public float Delay = 10f;

    /// <summary>
    /// How far from the group the other can be, for it to join, in tiles.
    /// </summary>
    [DataField]
    public float MaxDistance = 30f;
}

public sealed partial class MergeGroupsEntityEffectsSystem : EntityEffectSystem<MathVariablesComponent, MergeGroups>
{
    [Dependency] private GroupsSystem _groups = null!;
    [Dependency] private MathCurvesSystem _curves = null!;
    [Dependency] private MathVariablesSystem _variables = null!;
    [Dependency] private IGameTiming _timing = null!;

    private const string WeakSinceVariable = "weakSince";

    protected override void Effect(Entity<MathVariablesComponent> ent, ref EntityEffectEvent<MergeGroups> args)
    {
        var effect = args.Effect;
        var now = (float) _timing.CurTime.TotalSeconds;
        var variables = _variables.GetVariables(ent.AsNullable());
        var groups = _groups.Prune(ent, effect.Group);

        if (groups.Count < 2)
            return;

        var positions = groups.ToDictionary(x => x.Owner, x => _groups.Center(x.Comp));
        var taken = new HashSet<EntityUid>();

        // The weakest go first.
        foreach (var group in groups.OrderBy(x => _variables.Get(x.Owner, "power")))
        {
            if (taken.Contains(group.Owner) || !IsWeak(ent, group.Owner, effect, variables, now))
                continue;

            Entity<EngagementComponent>? closest = null;
            var closestDistance = effect.MaxDistance;

            foreach (var other in groups)
            {
                if (other.Owner == group.Owner || taken.Contains(other.Owner))
                    continue;

                var distance = Vector2.Distance(positions[group.Owner], positions[other.Owner]);

                if (distance >= closestDistance)
                    continue;

                closest = other;
                closestDistance = distance;
            }

            if (closest is not { } target)
                continue;

            _groups.Merge(group, target);
            _variables.Set(target.Owner, WeakSinceVariable, 0f);
            taken.Add(group.Owner);
            taken.Add(target.Owner);
        }
    }

    /// <summary>
    /// Has the group been weak for long enough.
    /// </summary>
    private bool IsWeak(
        Entity<MathVariablesComponent> ent,
        EntityUid group,
        MergeGroups effect,
        Dictionary<string, float> variables,
        float now)
    {
        var groupVariables = new Dictionary<string, float>(variables)
        {
            ["power"] = _variables.Get(group, "power"),
            ["ratio"] = _variables.Get(group, "ratio"),
        };

        if (_curves.Get(effect.Weak, 0f, ent, groupVariables) <= 0.5f)
        {
            _variables.Set(group, WeakSinceVariable, 0f);
            return false;
        }

        var since = _variables.Get(group, WeakSinceVariable);

        if (since <= 0f)
        {
            _variables.Set(group, WeakSinceVariable, Math.Max(now, 0.001f));
            return false;
        }

        return now - since >= effect.Delay;
    }
}
