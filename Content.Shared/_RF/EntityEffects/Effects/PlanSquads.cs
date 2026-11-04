using System.Linq;
using Content.Shared._RF.MathHelpers.MathCurve;
using Content.Shared._RF.MathHelpers.MathCurve.Systems;
using Content.Shared._RF.MathHelpers.MathVariables;
using Content.Shared._RF.NPC.Engagement.Components;
using Content.Shared._RF.NPC.Engagement.Prototypes;
using Content.Shared._RF.NPC.Engagement.Systems;
using Content.Shared._RF.NPC.Systems;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.EntityEffects.Effects;

/// <summary>
/// Splits the participants of the situations of a kind that the target owns (the raiders) into squads:
/// situations of another kind that the target owns too. It is for the start of the raid: it takes all the squads apart if they are
/// not right, which makes the raiders change places, so what happens later is up to the effects that change the squads a little,
/// such as <see cref="MergeWeakSquads"/>.
/// </summary>
public sealed partial class PlanSquads : EntityEffectBase<PlanSquads>
{
    /// <summary>
    /// The kind of the situations that the squads are made of the participants of.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId<EngagementComponent> Source;

    /// <summary>
    /// The role of the participants who are put into squads, all the roles if it is not set.
    /// </summary>
    [DataField]
    public ProtoId<EngagementRolePrototype>? SourceRole;

    /// <summary>
    /// The kind of the squads.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId<EngagementComponent> Squad;

    /// <summary>
    /// How strong a fighter is, calculated with the fighter as the user. It is what the squads are shared out by, and the strongest
    /// of the fighters are put into the squads first, so they take the roles that have to be filled.
    /// </summary>
    [DataField(required: true)]
    public List<MathCurve> Power = new();

    /// <summary>
    /// How strong a squad has to be at least. The curves see the variables of the target, so it can be what the squad is
    /// against, such as the power of the residents, times how much stronger it has to be.
    /// </summary>
    [DataField(required: true)]
    public List<MathCurve> MinPower = new();

    /// <summary>
    /// The most squads there can be, however strong the force is.
    /// </summary>
    [DataField]
    public int MaxSquads = 4;

    /// <summary>
    /// Does nothing if there are squads already (with participants): the effect then only makes the squads that are not there yet,
    /// once, and what happens to them later is up to the others.
    /// </summary>
    [DataField]
    public bool OnlyIfNone;

    /// <summary>
    /// The variable of the target that is 1 while the squads are being made, and 0 otherwise. The squads that are disbanded say
    /// that their leaders have left, which is not what the reactions to it are for.
    /// </summary>
    [DataField]
    public string PlanningVariable = "planning";
}

public sealed partial class PlanSquadsEntityEffectsSystem : EntityEffectSystem<MathVariablesComponent, PlanSquads>
{
    [Dependency] private EngagementSystem _engagement = null!;
    [Dependency] private OwnershipSystem _ownership = null!;
    [Dependency] private MathCurvesSystem _curves = null!;
    [Dependency] private MathVariablesSystem _variables = null!;

    [Dependency] private EntityQuery<EngagementComponent> _engagementQuery;

    protected override void Effect(Entity<MathVariablesComponent> ent, ref EntityEffectEvent<PlanSquads> args)
    {
        var effect = args.Effect;
        var fighters = new List<(EntityUid Uid, float Power)>();
        var squads = new List<Entity<EngagementComponent>>();

        foreach (var owned in _ownership.GetOwned(ent).ToArray())
        {
            if (!_engagementQuery.TryComp(owned, out var comp))
                continue;

            if (Prototype(owned)?.ID == effect.Squad.Id)
            {
                squads.Add((owned, comp));
                continue;
            }

            if (Prototype(owned)?.ID != effect.Source.Id)
                continue;

            foreach (var (role, actors) in comp.Actors)
            {
                if (effect.SourceRole != null && effect.SourceRole != role)
                    continue;

                foreach (var actor in actors)
                {
                    // Who is down is not a fighter, it is left where it is.
                    var power = _curves.Get(effect.Power, 0f, actor);

                    if (power > 0f)
                        fighters.Add((actor, power));
                }
            }
        }

        // The squads whose participants are all gone are over.
        foreach (var squad in squads.ToArray())
        {
            if (squad.Comp.Actors.Values.Any(x => x.Count > 0))
                continue;

            squads.Remove(squad);
            _engagement.EndEngagement(squad.AsNullable(), EngagementEndReason.Dissolved);
        }

        if (fighters.Count == 0 || effect.OnlyIfNone && squads.Count > 0)
            return;

        var total = fighters.Sum(x => x.Power);
        var minPower = _curves.Get(effect.MinPower, 0f, ent, _variables.GetVariables(ent.AsNullable()));
        var count = minPower > 0f ? (int) MathF.Floor(total / minPower) : effect.MaxSquads;
        count = Math.Clamp(count, 1, Math.Max(1, Math.Min(effect.MaxSquads, fighters.Count)));

        if (IsPlanned(squads, fighters, count))
            return;

        _variables.Set(ent.AsNullable(), effect.PlanningVariable, 1f);

        foreach (var squad in squads)
        {
            _engagement.EndEngagement(squad.AsNullable(), EngagementEndReason.Dissolved);
        }

        fighters.Sort((a, b) => b.Power.CompareTo(a.Power));

        // The groups: the strongest are the first of each, the others go to the weakest group.
        var groups = new List<(List<EntityUid> Members, float Power)>();

        for (var i = 0; i < count; i++)
        {
            groups.Add((new List<EntityUid> { fighters[i].Uid }, fighters[i].Power));
        }

        for (var i = count; i < fighters.Count; i++)
        {
            var weakest = 0;

            for (var j = 1; j < groups.Count; j++)
            {
                if (groups[j].Power < groups[weakest].Power)
                    weakest = j;
            }

            groups[weakest].Members.Add(fighters[i].Uid);
            groups[weakest] = (groups[weakest].Members, groups[weakest].Power + fighters[i].Power);
        }

        foreach (var (members, _) in groups)
        {
            // The system gives the roles that must be filled to those who are first, the strongest.
            if (!_engagement.TryStartEngagement(effect.Squad, members[0], members, out var squad)
                && !_engagement.TryStartEngagement(effect.Squad, members[0], out squad))
                continue;

            _ownership.AddOwnership(squad.Value, owner: ent);

            foreach (var member in members)
            {
                if (!_engagement.IsMember(squad.Value.AsNullable(), member))
                    _engagement.TryJoinAnyRole(squad.Value.AsNullable(), member);
            }
        }

        _variables.Set(ent.AsNullable(), effect.PlanningVariable, 0f);
    }

    /// <summary>
    /// Are the squads there already what they should be: as many as needed, and every fighter is in one.
    /// </summary>
    private static bool IsPlanned(
        List<Entity<EngagementComponent>> squads,
        List<(EntityUid Uid, float Power)> fighters,
        int count)
    {
        var members = new HashSet<EntityUid>();
        var used = 0;

        foreach (var squad in squads)
        {
            var any = false;

            foreach (var actors in squad.Comp.Actors.Values)
            {
                members.UnionWith(actors);
                any |= actors.Count > 0;
            }

            if (any)
                used++;
        }

        return used == count && fighters.All(x => members.Contains(x.Uid));
    }
}
