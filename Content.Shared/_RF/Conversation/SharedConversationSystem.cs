using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._RF.Conversation;

public abstract class SharedConversationSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    private readonly List<(
        ProtoId<ConversationScriptPrototype> Script,
        Dictionary<string, EntityUid> Actors,
        int Act,
        int Line)> _conversations = new();

    /// <summary>
    /// Starts a conversation between entities
    /// </summary>
    /// <param name="protoId">Conversation script prototype</param>
    /// <param name="uids">List of entities that will participate in the conversation</param>
    /// <returns>True, if the conversation has been successfully initiated</returns>
    public bool TryStartConversation(ProtoId<ConversationScriptPrototype> protoId, List<EntityUid> uids)
        => TryStartConversation(protoId, uids, out _);


    /// <summary>
    /// Starts a conversation between entities
    /// </summary>
    /// <param name="protoId">Conversation script prototype</param>
    /// <param name="uids">List of entities that will participate in the conversation</param>
    /// <param name="actors">Dictionary mapping roleId -> EntityUid, or null if no valid assignment exists</param>
    /// <returns>True, if the conversation has been successfully initiated</returns>
    public bool TryStartConversation(
        ProtoId<ConversationScriptPrototype> protoId,
        List<EntityUid> uids,
        [NotNullWhen(true)] out Dictionary<string, EntityUid>? actors)
    {
        actors = null;

        if (!_prototype.TryIndex(protoId, out var script)
            || uids.Count != script.Actors.Count
            || uids.Count == 0)
            return false;

        // check whether entities are involved in other dialogues
        foreach (var (_, acts, _, _) in _conversations)
        {
            foreach (var uid in uids)
            {
                if (acts.ContainsValue(uid))
                    return false;
            }
        }

        if (!TryFindRoles(script, uids, out actors))
            return false;

        _conversations.Add((protoId, actors, 0, 0));
        return true;
    }

    /// <summary>
    /// Returns the line of conversation that the entity should say.
    /// </summary>
    public bool TryGetLine(EntityUid uid, [NotNullWhen(true)] out string? line)
    {
        line = null;
        return TryGetScript(uid, out var script) && TryGetLine(script.Value, uid, out line);
    }

    /// <summary>
    /// Returns the line of conversation that the entity should say.
    /// </summary>
    public bool TryGetLine(ProtoId<ConversationScriptPrototype> protoId, EntityUid uid, [NotNullWhen(true)] out string? line)
    {
        line = null;

        if (!_prototype.TryIndex(protoId, out var script))
            return false;

        foreach (var (proto, actors, act, lineInd) in _conversations.ToList())
        {
            if (protoId != proto || !script.Dialog.TryGetValue(act, out var lines))
                continue;

            // find the ID of the actor in the conversation for the entity
            var actorId = actors.FirstOrNull(x => x.Value == uid)?.Key;

            if (actorId == null)
                return false;

            var i = 0;
            foreach (var (actor, locId) in lines)
            {
                if (i == lineInd)
                {
                    if (actor != actorId)
                        return false;

                    line = Loc.GetString(locId);
                    return true;
                }

                i++;
            }

            return false;
        }

        return false;
    }

    /// <summary>
    /// Moves to the next line of conversation in which the entity participates.
    /// </summary>
    protected void ContinueConversation(ProtoId<ConversationScriptPrototype> protoId, EntityUid uid)
    {
        if (!_prototype.TryIndex(protoId, out var script))
            return;

        foreach (var (proto, actors, act, lineInd) in _conversations.ToList())
        {
            if (protoId != proto
                || !actors.ContainsValue(uid)
                || !script.Dialog.TryGetValue(act, out var lines))
                continue;

            var newAct = act;
            var newLine = lineInd + 1;

            if (newLine >= lines.Count)
            {
                newLine = 0;
                newAct++;
            }

            _conversations.Remove((protoId, actors, act, lineInd));

            // check if the dialog is complete
            if (newAct < script.Dialog.Count)
            {
                _conversations.Add((protoId, actors, newAct, newLine));
                return;
            }

            // apply conversation completion effects
            foreach (var (id, effects) in script.Effects)
            {
                if (!actors.TryGetValue(id, out var actor))
                    continue;

                foreach (var effect in effects)
                {
                    effect.Effect(new EntityEffectBaseArgs(actor, EntityManager));
                }
            }

            return;
        }
    }

    /// <summary>
    /// Ends the conversation in which the entity participates
    /// </summary>
    public void EndConversation(EntityUid uid)
    {
        if (!TryGetScript(uid, out var script)
            || !_prototype.TryIndex(script, out var proto))
            return;

        for (var i = 0; i < _conversations.Count - 1; i++)
        {
            if (_conversations[i].Script != script || !_conversations[i].Actors.ContainsValue(uid))
                continue;

            // apply conversation completion effects
            foreach (var (id, effects) in proto.Effects)
            {
                if (!_conversations[i].Actors.TryGetValue(id, out var actor))
                    continue;

                foreach (var effect in effects)
                {
                    effect.Effect(new EntityEffectBaseArgs(actor, EntityManager));
                }
            }

            _conversations.RemoveAt(i);
            return;
        }
    }

    /// <summary>
    /// Checks whether the entity is next in line in the conversation
    /// </summary>
    public bool IsNextInConversation(EntityUid uid)
    {
        foreach (var (protoId, actors, act, lineInd) in _conversations)
        {
            var roleId = actors.FirstOrNull(x => x.Value == uid)?.Key;

            if (roleId == null)
                continue;

            if (!_prototype.TryIndex(protoId, out var script)
                || script.Dialog.TryGetValue(act, out var lines))
                return false;

            var i = 0;
            foreach (var (actor, _) in lines)
            {
                if (i == lineInd)
                    return actor == roleId;

                i++;
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// Returns the role ID of the entity in the conversation in which it participates
    /// </summary>
    public bool TryGetRole(EntityUid uid, [NotNullWhen(true)] out string? roleId)
    {
        roleId = null;

        foreach (var (_, actors, _, _) in _conversations)
        {
            roleId = actors.FirstOrNull(x => x.Value == uid)?.Key;

            if (roleId != null)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Returns the conversation script in which the entity participates.
    /// </summary>
    public bool TryGetScript(EntityUid uid, [NotNullWhen(true)] out ProtoId<ConversationScriptPrototype>? script)
    {
        script = null;

        foreach (var (protoId, actors, _, _) in _conversations)
        {
            if (!actors.ContainsValue(uid))
                continue;

            script = protoId;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Attempts to assign conversation roles to entities according to all actor requirements
    /// </summary>
    /// <param name="script"></param>
    /// <param name="uids"></param>
    /// <param name="roles">Dictionary mapping roleId -> EntityUid, or null if no valid assignment exists</param>
    public bool TryFindRoles(
        ConversationScriptPrototype script,
        List<EntityUid> uids,
        [NotNullWhen(true)] out Dictionary<string, EntityUid>? roles)
    {
        roles = null;

        var roleList = script.Actors.Select(a => a.Id).ToList();
        var actors = new Dictionary<string, EntityUid>();
        var used = new HashSet<EntityUid>();

        if (!Assign(0))
            return false;

        roles = actors;
        return true;

        // Checks the requirements defined by 'role' toward other already assigned roles.
        // For example: RoleA.Requirements[RoleB] must hold for (uid -> RoleA, assigned[RoleB]).
        bool CheckRoleRequirements(
            string role,
            EntityUid uid,
            Dictionary<string, EntityUid> assigned)
        {
            var roleData = script.Actors.FirstOrDefault(a => a.Id == role);

            if (roleData == null)
                return false;

            foreach (var (otherRole, reqList) in roleData.Requirements)
            {
                if (!assigned.TryGetValue(otherRole, out var otherUid))
                    continue;

                foreach (var req in reqList)
                {
                    if (!req.Check(uid, otherUid, EntityManager))
                        return false;
                }
            }

            return true;
        }

        // Checks the requirements that already assigned roles have toward the new role.
        // For example: If RoleB requires RoleA, then when assigning RoleA = uid,
        // we must validate RoleB.Requirements[RoleA].
        bool CheckReverseRequirements(
            string role,
            EntityUid uid,
            Dictionary<string, EntityUid> assigned)
        {
            foreach (var (otherRole, otherUid) in assigned)
            {
                var otherData = script.Actors.FirstOrDefault(a => a.Id == otherRole);

                if (otherData == null || !otherData.Requirements.TryGetValue(role, out var reqList))
                    continue;

                foreach (var req in reqList)
                {
                    if (!req.Check(otherUid, uid, EntityManager))
                        return false;
                }
            }

            return true;
        }

        // Recursively assigns each role to one entity using DFS backtracking.
        // Ensures all requirement constraints remain satisfied at each step.
        bool Assign(int index)
        {
            if (index >= roleList.Count)
                return true;

            var role = roleList[index];

            foreach (var uid in uids)
            {
                if (used.Contains(uid)
                    || !CheckRoleRequirements(role, uid, actors)
                    || !CheckReverseRequirements(role, uid, actors))
                    continue;

                used.Add(uid);
                actors[role] = uid;

                if (Assign(index + 1))
                    return true;

                used.Remove(uid);
                actors.Remove(role);
            }

            return false;
        }
    }
}
