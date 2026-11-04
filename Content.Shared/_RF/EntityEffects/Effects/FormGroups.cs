using System.Linq;
using Content.Shared._RF.MathHelpers.Assignment;
using Content.Shared._RF.MathHelpers.MathCurve;
using Content.Shared._RF.MathHelpers.MathCurve.Systems;
using Content.Shared._RF.MathHelpers.MathVariables;
using Content.Shared._RF.NPC.Engagement.Components;
using Content.Shared._RF.NPC.Engagement.Prototypes;
using Content.Shared._RF.NPC.Groups.Systems;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.EntityEffects.Effects;

/// <summary>
/// Divides the participants of the situations of a kind that the target owns (the raiders, the people of a caravan) into groups:
/// situations of another kind that the target owns too, see <see cref="GroupsSystem.Form"/>. It is for the start: it takes all
/// the groups apart if they are not right, which makes the members change places, so what happens later is up to the effects that
/// change the groups a little, such as <see cref="MergeGroups"/>.
/// </summary>
public sealed partial class FormGroups : EntityEffectBase<FormGroups>
{
    /// <summary>
    /// The kind of the situations that the groups are made of the participants of.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId<EngagementComponent> Source;

    /// <summary>
    /// The role of the participants who are put into groups, all the roles if it is not set.
    /// </summary>
    [DataField]
    public ProtoId<EngagementRolePrototype>? SourceRole;

    /// <summary>
    /// The kind of the groups.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId<EngagementComponent> Group;

    /// <summary>
    /// How strong a member is, calculated with the member as the user. It is what the groups are shared out by, and the strongest
    /// of the members are put into the groups first, so they take the roles that have to be filled. Who has nothing (not more than 0)
    /// is not a member of any group, such as the one who is down.
    /// </summary>
    [DataField(required: true)]
    public List<MathCurve> Power = new();

    /// <summary>
    /// How strong a group has to be at least. The curves see the variables of the target, so it can be what the group is
    /// against, such as the power of the residents, times how much stronger it has to be.
    /// </summary>
    [DataField(required: true)]
    public List<MathCurve> MinPower = new();

    /// <summary>
    /// The most groups there can be, however strong the members are.
    /// </summary>
    [DataField]
    public int MaxGroups = 4;

    /// <summary>
    /// Does nothing if there are groups already (with participants): the effect then only makes the groups that are not there yet,
    /// once, and what happens to them later is up to the others.
    /// </summary>
    [DataField]
    public bool OnlyIfNone;
}

public sealed partial class FormGroupsEntityEffectsSystem : EntityEffectSystem<MathVariablesComponent, FormGroups>
{
    [Dependency] private GroupsSystem _groups = null!;
    [Dependency] private EntityScoringSystem _scoring = null!;
    [Dependency] private MathCurvesSystem _curves = null!;
    [Dependency] private MathVariablesSystem _variables = null!;

    protected override void Effect(Entity<MathVariablesComponent> ent, ref EntityEffectEvent<FormGroups> args)
    {
        var effect = args.Effect;
        var sources = _groups.GetGroups(ent, effect.Source).SelectMany(x => _groups.GetMembers(x.Comp, effect.SourceRole));
        var members = _scoring.Score(sources, effect.Power, positiveOnly: true);

        // The groups whose participants are all gone are over.
        var groups = _groups.Prune(ent, effect.Group);

        if (members.Count == 0 || effect.OnlyIfNone && groups.Count > 0)
            return;

        var total = members.Sum(x => x.Score);
        var minPower = _curves.Get(effect.MinPower, 0f, ent, _variables.GetVariables(ent.AsNullable()));
        var count = minPower > 0f ? (int) MathF.Floor(total / minPower) : effect.MaxGroups;
        count = Math.Clamp(count, 1, Math.Max(1, Math.Min(effect.MaxGroups, members.Count)));

        if (_groups.IsFormed(groups, members.Select(x => x.Uid), count))
            return;

        _groups.Form(ent, effect.Group, members.Select(x => (x.Uid, x.Score)), count);
    }
}
