using System.Diagnostics.CodeAnalysis;
using Content.Server._RF.NPC.Systems;
using Content.Server._RF.Workshops.Components;
using Content.Shared._RF.NPC.Components;
using Content.Shared._RF.Skills;
using Content.Shared._RF.Workshops.Components;
using Content.Shared._RF.Workshops.Systems;
using Content.Shared.Inventory;

namespace Content.Server._RF.Workshops.Systems;

public sealed partial class WorkshopSystem : SharedWorkshopSystem
{
    [Dependency] private readonly NpcHelperSystem _npcHelper = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;

    private EntityQuery<WorkshopTaskSourceComponent> _query;

    public override void Initialize()
    {
        base.Initialize();

        /* TODO
        SubscribeLocalEvent<WorkshopComponent, NpcTaskGivenTarget>(OnTaskGiven);
        SubscribeLocalEvent<WorkshopComponent, NpcTaskFinishedTarget>(OnTaskFinished);
        */

        _query = GetEntityQuery<WorkshopTaskSourceComponent>();
    }

    /* TODO
    private void OnTaskGiven(Entity<WorkshopComponent> ent, ref NpcTaskGivenTarget args)
    {
        if (!_query.TryComp(ent, out var source) || source.Task != args.Task)
            return;

        if (!TryStartCrafting(ent.AsNullable()))
            UpdateUi(ent.AsNullable());
    }

    private void OnTaskFinished(Entity<WorkshopComponent> ent, ref NpcTaskFinishedTarget args)
    {
        if (!_query.TryComp(ent, out var source) || source.Task != args.Task)
            return;

        if (source.SuspendOnFail && args.Status == TaskFinishStatus.Failed)
            SetSuspend(ent.AsNullable(), ent.Comp.Queue.Index, true);
        else if (ent.Comp.Crafting)
            StopCrafting(ent.AsNullable(), false);
    }
    */

    protected override void UpdateNpcRecipe(EntityUid uid)
    {
        /* TODO
        if (!_query.TryComp(uid, out var comp)
            || !_npcControl.TryGetUser(comp.Task, uid, out var npc)
            || !TryComp(npc, out HTNComponent? htn)
            || GetCurrentRecipe(uid) is not { } protoId)
            return;

        htn.Blackboard.SetValue(comp.TargetRecipeKey, protoId);
        */
    }

    protected override void AddPassiveTask(Entity<WorkshopComponent?> ent)
    {
        /* TODO
        if (!Resolve(ent, ref ent.Comp)
            || !_query.TryComp(ent, out var comp)
            || GetQueueRecipe(ent.Comp, 0) is not { } protoId)
            return;

        if (HasComp<PassiveGoalTargetComponent>(ent))
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
                additionalKeys: new() { { comp.TargetRecipeKey, protoId } });
            break;
        }
        */
    }

    protected override void FinishTask(Entity<WorkshopComponent?> ent)
    {
        /* TODO
        if (!Resolve(ent, ref ent.Comp)
            || !_query.TryComp(ent, out var source)
            || !_npcControl.TryGetUser(source.Task, ent, out var npc))
            return;

        _npcControl.FinishTask(npc.Value);
        */
        UpdateUi(ent);
    }

    protected override void RemovePassiveTask(EntityUid ent)
    {
        RemComp<PassiveGoalTargetComponent>(ent);
    }

    public override bool TryGetUser(Entity<WorkshopComponent?> ent, [NotNullWhen(true)] out EntityUid? user)
    {
        user = null;
        /* TODO
        if (!Resolve(ent, ref ent.Comp)
            || !_query.TryComp(ent, out var source)
            || !TryComp(ent, out ActiveNpcTaskTargetComponent? target)
            || !target.Tasks.TryGetValue(source.Task, out var users))
            return false;

        user = users.FirstOrNull();
        */
        return user != null;
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
