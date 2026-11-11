using System.Linq;
using Content.Shared._RF.MathHelpers.Assignment;
using Content.Shared._RF.MathHelpers.MathCurve;
using Content.Shared._RF.MathHelpers.MathCurve.Systems;
using Content.Shared._RF.MathHelpers.MathVariables;
using Content.Shared._RF.NPC.Engagement.Components;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;
using Content.Shared._RF.NPC.Groups.Systems;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.EntityEffects.Effects;

/// <summary>
/// Shares the targets between the groups that the target owns, see <see cref="AssignmentSystem"/>: every target is given to one
/// group, and every group has its share in a key of the state of the group, which its participants read as
/// <c>Engagement/RaidSquad/State/Objective</c>. A target goes to the group it costs the least, as the cost curves say: that can be
/// how far it is from the group, and how much of the targets the group has already got compared with its strength (<c>load</c> and
/// <c>capacity</c>). The strongest targets are shared first. A target that a group has stays with it: only those that nobody has are
/// shared, so it can be applied as often as the groups or the targets may have changed, and nothing is passed over.
/// </summary>
public sealed partial class AssignGroupTargets : EntityEffectBase<AssignGroupTargets>
{
    /// <summary>
    /// The kind of the groups. They must have a state (<c>Goap</c>), and the variables that the curves look at, such as the
    /// <c>power</c> of <see cref="MeasureGroups"/>.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId<EngagementComponent> Group;

    /// <summary>
    /// The key of the state of the target with the targets, those that are to be shared.
    /// </summary>
    [DataField(required: true)]
    public StateKey<IReadOnlyCollection<EntityUid>> Targets;

    /// <summary>
    /// The key of the state of a group that its share is put into.
    /// </summary>
    [DataField]
    public StateKey<HashSet<EntityUid>> Key = "Objective";

    /// <summary>
    /// How strong a target is, calculated with the target as the user. It is the weight of the target in the share.
    /// </summary>
    [DataField(required: true)]
    public List<MathCurve> Power = new();

    /// <summary>
    /// How much a group is meant to take, calculated with the group as the user (so its variables are the ones that the curves see
    /// as the user's) and the variables of the target of the effect. It is the <c>capacity</c> of the cost. The <c>power</c> of the
    /// group if it is not set.
    /// </summary>
    [DataField]
    public List<MathCurve> Capacity = new();

    /// <summary>
    /// The most that a group can take, in the power of the targets, calculated the same way as <see cref="Capacity"/>. A group
    /// takes as many as the cost says if it is not set, and the targets that do not fit any are left alone.
    /// </summary>
    [DataField]
    public List<MathCurve> MaxLoad = new();

    /// <summary>
    /// What it costs a group to take a target, calculated with the target as the user. The least cost wins. The curves see the
    /// variables of the target of the effect, those of the group, and: <c>distance</c> from the group to the target, <c>load</c> the
    /// power of the targets the group has got already, <c>capacity</c> of the group, <c>count</c> of the targets it has got,
    /// <c>weight</c> of the target.
    /// </summary>
    [DataField(required: true)]
    public List<MathCurve> Cost = new();
}

public sealed partial class AssignGroupTargetsEntityEffectsSystem : EntityEffectSystem<GoapComponent, AssignGroupTargets>
{
    [Dependency] private SharedGoapSystem _goap = null!;
    [Dependency] private GroupsSystem _groups = null!;
    [Dependency] private AssignmentSystem _assignment = null!;
    [Dependency] private MathCurvesSystem _curves = null!;
    [Dependency] private MathVariablesSystem _variables = null!;

    [Dependency] private EntityQuery<GoapComponent> _goapQuery;

    /// <summary>
    /// The power that a group has, so the load is a number.
    /// </summary>
    private const float MinCapacity = 0.01f;

    protected override void Effect(Entity<GoapComponent> ent, ref EntityEffectEvent<AssignGroupTargets> args)
    {
        var effect = args.Effect;
        var variables = _variables.GetVariables(ent.Owner);
        var buckets = new List<AssignmentBucket<EntityUid>>();
        var goaps = new Dictionary<EntityUid, GoapComponent>();
        var shares = new Dictionary<EntityUid, HashSet<EntityUid>>();

        foreach (var (owned, comp) in _groups.GetGroups(ent, effect.Group))
        {
            if (!_goapQuery.TryComp(owned, out var goap) || _groups.IsEmpty(comp))
                continue;

            var groupVariables = new Dictionary<string, float>(variables);

            foreach (var (key, value) in _variables.GetVariables(owned))
            {
                groupVariables[key] = value;
            }

            var capacity = effect.Capacity.Count == 0
                ? _variables.Get(owned, "power")
                : _curves.Get(effect.Capacity, 0f, owned, groupVariables);

            buckets.Add(new AssignmentBucket<EntityUid>(owned, Math.Max(MinCapacity, capacity))
            {
                Position = _groups.Center(comp),
                Variables = groupVariables,
                MaxLoad = effect.MaxLoad.Count == 0 ? null : _curves.Get(effect.MaxLoad, 0f, owned, groupVariables),
            });
            goaps[owned] = goap;

            if (SharedGoapSystem.TryGetValueNoEcsDefaults(goap.State, effect.Key, out var share))
                shares[owned] = share;
        }

        if (buckets.Count == 0)
            return;

        _goap.TryGetValue(ent.Comp.State, effect.Targets, out var targets);

        // Who has a target keeps it: the targets are not passed from group to group, and the groups do not run across the settlement
        // because of a loss somewhere. Only the targets that nobody has are given out: the ones that were the share of a group that is
        // gone, and the new ones.
        var result = _assignment.AssignEntities(
            targets ?? Array.Empty<EntityUid>(),
            buckets,
            effect.Power,
            effect.Cost,
            current: target => shares.FirstOrDefault(x => x.Value.Contains(target)).Key is { Valid: true } owner ? owner : null,
            variables: variables);

        foreach (var (group, taken) in result.Taken)
        {
            // Only what has changed is put in: the queries of the participants follow the key.
            var share = taken.ToHashSet();

            if (shares.TryGetValue(group, out var old) && old.SetEquals(share))
                continue;

            _goap.SetValue(goaps[group].State, effect.Key, share);
        }
    }
}
