using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using Content.Shared._RF.Conversation.Components;
using Content.Shared._RF.NPC.Engagement;
using Content.Shared._RF.NPC.Engagement.Components;
using Content.Shared._RF.NPC.Engagement.Prototypes;
using Content.Shared._RF.NPC.Engagement.Systems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared.Chat;
using Content.Shared.Mobs;
using JetBrains.Annotations;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Shared._RF.Conversation.Systems;

/// <summary>
/// A helper system for easily implementing advanced random conversations between NPCs.
/// </summary>
public sealed class ConversationSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly EngagementSystem _engagement = default!;

    [Dependency] private readonly EntityQuery<ConversationActorComponent> _actorQuery = default!;
    [Dependency] private readonly EntityQuery<ConversationComponent> _conversationQuery = default!;
    [Dependency] private readonly EntityQuery<EngagementComponent> _engagementQuery = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ConversationActorComponent, ComponentRemove>(OnActorRemove);
        SubscribeLocalEvent<ConversationActorComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<ConversationComponent, EngagementStarted>(OnEngagementStarted);
        SubscribeLocalEvent<ConversationComponent, EngagementEnded>(OnEngagementEnded);
        SubscribeLocalEvent<ConversationComponent, EngagementRoleJoined>(OnEngagementJoined);
        SubscribeLocalEvent<ConversationComponent, EngagementRoleLeft>(OnEngagementLeft);
    }

    private void OnActorRemove(Entity<ConversationActorComponent> ent, ref ComponentRemove args)
    {
        if (!TryGetConversation(ent.AsNullable(), out var conv))
            return;

        // Leaving one role is enough - if the underlying EngagementPrototype has
        // DissolveInvalid set (which every conversation kind should), the Engagement
        // system itself cascades this into EndEngagement for the remaining actors.
        _engagement.LeaveEngagement(conv.Value.Owner, ent, EngagementEndReason.Interrupted);
    }

    private void OnMobStateChanged(Entity<ConversationActorComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Alive)
            EndConversation(ent.AsNullable());
    }

    /// <summary>
    /// Fires once every role of the underlying situation has reached its minimum participant
    /// count - i.e. once every invited actor with a consent-required role has accepted.
    /// Refreshes the cached role -&gt; actor mapping and kicks off the first line.
    /// </summary>
    private void OnEngagementStarted(Entity<ConversationComponent> ent, ref EngagementStarted args)
    {
        SetupActors(ent);
        StartConversation(ent);
    }

    /// <summary>
    /// The situation ended (naturally via <see cref="EndConversation"/>, or dissolved/interrupted
    /// by the Engagement system itself, e.g. one actor dying). Either way, only conversation-specific
    /// actor state needs cleaning up here - membership and GoapState effects are already handled.
    /// </summary>
    private void OnEngagementEnded(Entity<ConversationComponent> ent, ref EngagementEnded args)
    {
        if (!_engagementQuery.TryComp(ent, out var comp))
            return;

        foreach (var (_, uids) in comp.Actors)
        {
            foreach (var uid in uids)
            {
                RemComp<ConversationActorComponent>(uid);
            }
        }
    }

    private void OnEngagementJoined(Entity<ConversationComponent> ent, ref EngagementRoleJoined args)
    {
        var actor = EnsureComp<ConversationActorComponent>(args.Actor);
        actor.Conversation = ent;
        actor.Ready = false;
        actor.TargetPos = ent.Comp.StartPosition;
        actor.TargetRangeKey = GoapState.ConversationRange;
    }

    private void OnEngagementLeft(Entity<ConversationComponent> ent, ref EngagementRoleLeft args)
    {
        RemComp<ConversationActorComponent>(args.Actor);
    }

    private void SetupActors(Entity<ConversationComponent> ent)
    {
        if (!_engagementQuery.TryComp(ent, out var comp))
            return;

        foreach (var (_, uids) in comp.Actors)
        {
            foreach (var uid in uids)
            {
                var actor = EnsureComp<ConversationActorComponent>(uid);
                actor.Conversation = ent;
                actor.Ready = false;
                actor.TargetPos = ent.Comp.StartPosition;
                actor.TargetRangeKey = GoapState.ConversationRange;
            }
        }
    }

    /// <summary>
    /// Picks the first line and positions actors. Dissolves the situation if the script
    /// has no lines, or its first speaker never ended up seated.
    /// </summary>
    private void StartConversation(Entity<ConversationComponent> ent)
    {
        if (!_prototype.Resolve(ent.Comp.Script, out var script)
            || !_engagementQuery.TryComp(ent, out var comp)
            || GetNextMessage(script) is not { } first
            || !_engagement.TryGetActors(ent.Owner, first.Actor, out var firstActors)
            || firstActors.Count == 0)
        {
            _engagement.EndEngagement(ent.Owner, EngagementEndReason.Dissolved);
            return;
        }

        ent.Comp.NextActors = firstActors.ToHashSet();
        ent.Comp.NextMessage = first.Index;
        ent.Comp.NextDelay = first.Delay;
        ent.Comp.NextSpeakType = first.SpeakType;
        ent.Comp.NextSpeak = first.Speak;

        foreach (var (_, uids) in comp.Actors)
        {
            foreach (var uid in uids)
            {
                if (!_actorQuery.TryComp(uid, out var actor))
                    continue;

                actor.TargetFaceTo = firstActors.Contains(uid)
                    ? GetRotatePosition(ent.Comp, comp)
                    : ConversationCenter(comp);
            }
        }
    }

    private (int Index, ProtoId<EngagementRolePrototype> Actor, TimeSpan Delay, InGameICChatType SpeakType, bool Speak)? GetNextMessage(
        ConversationScriptPrototype script,
        int current = -1)
    {
        var next = current + 1;

        switch (script.Order)
        {
            case ConversationBasicOrderType seq:
                if (current >= seq.Lines - 1)
                    return null;

                var actor = seq.Actors[next % seq.Actors.Count];
                var delay = TimeSpan.FromSeconds(_random.NextFloat(seq.Delay.Min, seq.Delay.Max));
                return (next, actor, delay, seq.SpeakType, true);
            case ConversationCustomOrderType custom:
                if (current >= custom.Custom.Count - 1)
                    return null;

                var nextLine = custom.Custom[next];
                delay = TimeSpan.FromSeconds(nextLine.Delay?.Next(_random) ?? custom.Delay.Next(_random));
                return (next, nextLine.Id, delay, nextLine.SpeakType, nextLine.Speak);
            default:
                throw new ArgumentOutOfRangeException(nameof(ConversationScriptPrototype.Order), script.Order, null);
        }
    }

    private Vector2 GetRotatePosition(ConversationComponent conv, EngagementComponent engage)
    {
        var script = _prototype.Index(conv.Script);

        switch (script.Order)
        {
            case ConversationBasicOrderType:
                return ConversationCenter(engage);
            case ConversationCustomOrderType custom:
                var msg = custom.Custom[conv.NextMessage];

                if (msg.FaceDir != null)
                    return Transform(conv.NextActors.First()).Coordinates.Position + msg.FaceDir.Value;

                if (msg.FaceTo == null)
                    return ConversationCenter(engage);

                return Transform(engage.Actors[msg.FaceTo.Value].First()).Coordinates.Position;
            default:
                throw new ArgumentOutOfRangeException(nameof(ConversationScriptPrototype.Order), script.Order, null);
        }
    }

    private Vector2 ConversationCenter(EngagementComponent comp)
    {
        var pos = Vector2.Zero;
        var count = 0;

        foreach (var (_, uids) in comp.Actors)
        {
            foreach (var uid in uids)
            {
                pos += Transform(uid).Coordinates.Position;
                count++;
            }
        }

        pos /= count;
        return pos;
    }

    private bool ValidateConversation(Entity<ConversationComponent, EngagementComponent> ent)
    {
        if (!_prototype.HasIndex(ent.Comp1.Script)
            || ent.Comp2.Started && ent.Comp1.NextActors.Count == 0)
        {
            Invalid();
            return false;
        }

        foreach (var uid in ent.Comp1.NextActors)
        {
            if (_actorQuery.TryComp(uid, out var actor)
                && actor.Conversation == ent.Owner
                && _engagement.IsMember(ent.Owner, uid))
                continue;

            Invalid();
            return false;
        }

        foreach (var (_, uids) in ent.Comp2.Actors)
        {
            foreach (var uid in uids)
            {
                if (_actorQuery.TryComp(uid, out var actor)
                    && actor.Conversation == ent.Owner)
                    continue;

                Invalid();
                return false;
            }
        }

        return true;

        void Invalid()
        {
            Log.Debug($"terminating invalid conversation {ToPrettyString(ent)}");
            _engagement.EndEngagement(new(ent, ent.Comp2), EngagementEndReason.Dissolved);
        }
    }

    /// <summary>
    /// Starts a conversation with a pre-gathered set of candidate entities, on behalf of a specific
    /// <paramref name="initiator"/>.
    /// </summary>
    /// <remarks>
    /// Same role assignment/consent rules as the other overloads apply to every candidate except
    /// <paramref name="initiator"/> itself: since it is the one calling this method, it is treated as
    /// having already consented and is seated immediately even if its assigned role isn't
    /// <see cref="EngagementRolePrototype.Force"/> - its own invite, if any, is accepted on the spot.
    /// </remarks>
    /// <param name="protoId">Conversation script prototype.</param>
    /// <param name="initiator">The entity starting the conversation. Must be included in <paramref name="candidates"/>.</param>
    /// <param name="candidates">Candidate entities that may end up participating in the conversation.</param>
    /// <param name="actors">
    /// Role -&gt; entity mapping as it stands right after this call returns. May be incomplete if some
    /// roles required consent that hasn't been given yet.
    /// </param>
    [PublicAPI]
    public bool TryStartConversation(
        ProtoId<ConversationScriptPrototype> protoId,
        EntityUid initiator,
        HashSet<EntityUid> candidates,
        [NotNullWhen(true)] out Dictionary<ProtoId<EngagementRolePrototype>, HashSet<EntityUid>>? actors)
    {
        actors = null;

        if (!_prototype.TryIndex(protoId, out var script))
            return false;

        DebugTools.Assert(candidates.Contains(initiator));

        if (!_engagement.TryStartEngagement(script.Engagement, initiator, candidates, out var engagement))
            return false;

        var convEnt = engagement.Value.Owner;
        var convComp = EnsureComp<ConversationComponent>(convEnt);
        convComp.Script = protoId;
        convComp.NextActors = new();
        // TODO: I think the starting location for the conversation should be determined using a more advanced method
        convComp.StartPosition = Transform(initiator).Coordinates;
        actors = engagement.Value.Comp.Actors;
        SetupActors(new(convEnt, convComp));
        return true;
    }

    /// <summary>
    /// Returns the line of conversation that the entity should say.
    /// </summary>
    [PublicAPI, Pure]
    public bool TryGetLine(
        Entity<ConversationActorComponent?> ent,
        [NotNullWhen(true)] out string? line,
        [NotNullWhen(true)] out TimeSpan? delay,
        [NotNullWhen(true)] out InGameICChatType? speakType)
    {
        line = null;
        delay = null;
        speakType = null;

        if (!TryGetConversation(ent, out var conv)
            || conv.Value.Comp1.NextMessage < 0
            || !conv.Value.Comp1.NextSpeak)
            return false;

        line = Loc.GetString($"conversation-{conv.Value.Comp1.Script.Id.ToLowerInvariant()}-line-{Id()}");
        delay = conv.Value.Comp1.NextDelay;
        speakType = conv.Value.Comp1.NextSpeakType;
        return true;

        int Id()
        {
            var comp = conv.Value.Comp1;

            if (!_prototype.Resolve(comp.Script, out var script))
                return comp.NextMessage + 1;

            switch (script.Order)
            {
                case ConversationBasicOrderType:
                    return comp.NextMessage + 1;
                case ConversationCustomOrderType custom:
                    var id = 0;

                    for (var i = 0; i <= comp.NextMessage; i++)
                    {
                        if (custom.Custom[i].Speak)
                            id++;
                    }

                    return id;
                default:
                    throw new ArgumentOutOfRangeException(nameof(ConversationScriptPrototype.Order), script.Order, null);
            }
        }
    }

    /// <summary>
    /// Moves to the next line of conversation in which the entity participates.
    /// </summary>
    [PublicAPI]
    public void ContinueConversation(Entity<ConversationActorComponent?> ent)
    {
        if (!TryGetConversation(ent, out var conv)
            || !conv.Value.Comp1.NextActors.Contains(ent)
            || !_prototype.Resolve(conv.Value.Comp1.Script, out var script))
            return;

        var comp = conv.Value.Comp1;

        if (GetNextMessage(script, comp.NextMessage) is not { } next)
        {
            EndConversation(ent, true);
            return;
        }

        if (!_engagement.TryGetActors(conv.Value.Owner, next.Actor, out var nextActors))
        {
            EndConversation(ent);
            return;
        }

        comp.NextActors = nextActors.ToHashSet();
        comp.NextDelay = next.Delay;
        comp.NextMessage = next.Index;
        comp.NextSpeakType = next.SpeakType;
        comp.NextSpeak = next.Speak;

        foreach (var uid in comp.NextActors)
        {
            if (!_actorQuery.TryComp(uid, out var actor))
                return;

            if (script.Order is ConversationCustomOrderType custom
                && custom.Custom[comp.NextMessage].PosOffset is { } offset)
            {
                actor.TargetPos = new EntityCoordinates(comp.StartPosition.EntityId, comp.StartPosition.Position + offset);
                actor.TargetRangeKey = GoapState.MovementRange;
            }

            actor.TargetFaceTo = GetRotatePosition(comp, conv.Value.Comp2);
        }
    }

    /// <summary>
    /// Ends the conversation in which the entity participates, for every participant.
    /// </summary>
    /// <param name="ent">Any current participant of the conversation.</param>
    /// <param name="applyEffects">
    /// If true, applies each role's <see cref="EngagementRolePrototype.Effects"/> before tearing
    /// down the situation, and reports <see cref="EngagementEndReason.Finished"/> instead of
    /// <see cref="EngagementEndReason.Interrupted"/>.
    /// </param>
    [PublicAPI]
    public void EndConversation(Entity<ConversationActorComponent?> ent, bool applyEffects = false)
    {
        if (!Resolve(ent, ref ent.Comp, false)
            || !TryGetConversation(ent, out var conv))
            return;

        _engagement.EndEngagement(
            conv.Value.Owner,
            applyEffects ? EngagementEndReason.Finished : EngagementEndReason.Interrupted);
    }

    /// <summary>
    /// Updates the actor's readiness to engage in conversation.
    /// </summary>
    [PublicAPI]
    public void SetReady(Entity<ConversationActorComponent?> ent, bool ready)
    {
        if (Resolve(ent, ref ent.Comp))
            ent.Comp.Ready = ready;
    }

    /// <summary>
    /// Returns true if all actors have indicated their readiness to engage in a conversation.
    /// </summary>
    [PublicAPI]
    public bool AllReady(Entity<ConversationActorComponent?> ent)
    {
        if (!TryGetConversation(ent, out var conv)
            || !_engagementQuery.TryComp(conv, out var engage))
            return false;

        foreach (var (_, uids) in engage.Actors)
        {
            foreach (var uid in uids)
            {
                if (!_actorQuery.TryComp(uid, out var actor) || !actor.Ready)
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Checks whether the entity is next in line in the conversation.
    /// </summary>
    [PublicAPI, Pure]
    public bool IsNextInConversation(Entity<ConversationActorComponent?> ent)
        => TryGetConversation(ent, out var conv)
           && conv.Value.Comp2.Started
           && conv.Value.Comp1.NextActors.Contains(ent);

    /// <summary>
    /// Returns the conversation in which the entity is participating.
    /// </summary>
    [PublicAPI, Pure]
    public bool TryGetConversation(
        Entity<ConversationActorComponent?> ent,
        [NotNullWhen(true)] out Entity<ConversationComponent, EngagementComponent>? conversation)
    {
        conversation = null;

        if (!Resolve(ent, ref ent.Comp, false)
            || !_conversationQuery.TryComp(ent.Comp.Conversation, out var comp)
            || !_engagementQuery.TryComp(ent.Comp.Conversation, out var engage))
            return false;

        conversation = (ent.Comp.Conversation, comp, engage);
        return ValidateConversation(conversation.Value);
    }

    /// <summary>
    /// Checks whether all participants in the conversation are within a specified radius of the target location.
    /// </summary>
    /// <param name="ent">One of the participants in the conversation.</param>
    /// <param name="targetCoords">Target coordinates.</param>
    /// <param name="range">Maximum radius.</param>
    [PublicAPI]
    public bool ActorsInRange(
        Entity<ConversationActorComponent?> ent,
        EntityCoordinates targetCoords,
        float range)
    {
        if (!TryGetConversation(ent, out var conv)
            || !_engagementQuery.TryComp(conv, out var engage))
            return false;

        foreach (var (_, uids) in engage.Actors)
        {
            foreach (var uid in uids)
            {
                if (!_transform.InRange(Transform(uid).Coordinates, targetCoords, range))
                    return false;
            }
        }

        return true;
    }
}
