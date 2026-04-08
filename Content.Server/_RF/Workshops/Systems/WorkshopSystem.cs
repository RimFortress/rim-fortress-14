using System.Diagnostics.CodeAnalysis;
using Content.Server._RF.NPC.Components;
using Content.Server._RF.NPC.Systems;
using Content.Server._RF.Workshops.Components;
using Content.Server.NPC.HTN;
using Content.Shared._RF.Skills;
using Content.Shared._RF.Workshops.Components;
using Content.Shared._RF.Workshops.Systems;
using Content.Shared.Inventory;

namespace Content.Server._RF.Workshops.Systems;

public sealed partial class WorkshopSystem : SharedWorkshopSystem
{
    [Dependency] private readonly NpcControlSystem _npcControl = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;

    private EntityQuery<WorkshopTaskSourceComponent> _query;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WorkshopComponent, NpcTaskGivenTarget>(OnTaskGiven);
        SubscribeLocalEvent<WorkshopComponent, NpcTaskFinishedTarget>(OnTaskFinished);

        _query = GetEntityQuery<WorkshopTaskSourceComponent>();
    }

    private void OnTaskGiven(Entity<WorkshopComponent> ent, ref NpcTaskGivenTarget args)
    {
        if (!_query.TryComp(ent, out var source) || source.Task != args.Task)
            return;

        if (!TryStartCrafting(ent.AsNullable()))
            UpdateUi(ent.AsNullable());
    }

    private void OnTaskFinished(Entity<WorkshopComponent> ent, ref NpcTaskFinishedTarget args)
    {
        if (!_query.TryComp(ent, out var source)
            || source.Task != args.Task
            || !ent.Comp.Crafting)
            return;

        StopCrafting(ent.AsNullable());
    }

    protected override void UpdateNpcRecipe(EntityUid uid)
    {
        if (!_query.TryComp(uid, out var comp)
            || !_npcControl.TryGetUser(comp.Task, uid, out var npc)
            || !TryComp(npc, out HTNComponent? htn)
            || GetCurrentRecipe(uid) is not { } protoId)
            return;

        htn.Blackboard.SetValue(comp.TargetRecipeKey, protoId);
    }

    protected override void AddPassiveTask(Entity<WorkshopComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp)
            || !_query.TryComp(ent, out var comp)
            || GetQueueRecipe(ent.Comp, 0) is not { } protoId)
            return;

        if (HasComp<PassiveNpcTaskTargetComponent>(ent))
            return;

        foreach (var owner in Ownership.GetOwners(ent))
        {
            if (!HasComp<NpcControlComponent>(owner))
                continue;

            _npcControl.SetPassiveTaskTarget(
                owner,
                comp.Task,
                ent,
                removeWhenFailed: false,
                additionalKeys: new() { {comp.TargetRecipeKey, protoId} });
            break;
        }
    }

    protected override void FinishTask(Entity<WorkshopComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp)
            || !_query.TryComp(ent, out var source)
            || !_npcControl.TryGetUser(source.Task, ent, out var npc))
            return;

        _npcControl.FinishTask(npc.Value);
        UpdateUi(ent);
    }

    protected override void RemovePassiveTask(EntityUid ent)
    {
        RemComp<PassiveNpcTaskTargetComponent>(ent);
    }

    public override bool TryGetUser(Entity<WorkshopComponent?> ent, [NotNullWhen(true)] out EntityUid? user)
    {
        user = null;
        if (!Resolve(ent, ref ent.Comp)
            || !_query.TryComp(ent, out var source)
            || !_npcControl.TryGetUser(source.Task, ent, out user))
            return false;

        ent.Comp.User = user;
        return true;
    }

    public override EntityUid? GetUser(Entity<WorkshopComponent?> ent) => TryGetUser(ent, out var uid) ? uid : null;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var enumerator = EntityQueryEnumerator<WorkshopComponent>();
        while (enumerator.MoveNext(out var uid, out var comp))
        {
            if (!comp.Crafting || comp.Queue.Entry?.CraftingEndTime > Timing.CurTime)
                continue;

            var ent = new Entity<WorkshopComponent?>(uid, comp);

            if (!Proto.Resolve(GetCurrentRecipe(ent), out var proto))
            {
                StopCrafting(ent);
                continue;
            }

            foreach (var exp in proto.SkillsUp)
            {
                Skills.AddExperience(uid, exp);
            }

            DeleteIngredients(comp, proto);

            if (TryGetUser(ent, out var user) && Skills.DoInteractionCheck(uid, user.Value) == SkillCheckResult.Fail)
            {
                if (comp.CraftingFailResult != null)
                    SpawnResult(uid, comp.CraftingFailResult.Value);

                Audio.PlayPvs(comp.CraftingFailSound, uid);

                if (!TryStartCrafting(ent))
                    StopCrafting(ent);
            }
            else
            {
                SpawnResult(uid, proto.Result);
                AdvanceQueue(ent);
            }
        }
    }
}
