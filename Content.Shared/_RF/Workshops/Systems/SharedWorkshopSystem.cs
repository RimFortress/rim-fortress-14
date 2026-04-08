using System.Linq;
using Content.Shared._RF.NPC;
using Content.Shared._RF.Skills;
using Content.Shared._RF.Workshops.Components;
using Content.Shared._RF.Workshops.Prototypes;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Construction.EntitySystems;
using Content.Shared.Destructible;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Content.Shared.Stacks;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._RF.Workshops.Systems;

public abstract partial class SharedWorkshopSystem : EntitySystem
{
    [Dependency] protected readonly SharedSolutionContainerSystem Solution = default!;
    [Dependency] protected readonly SharedContainerSystem Container = default!;
    [Dependency] protected readonly OwnershipSystem Ownership = default!;
    [Dependency] protected readonly SharedSkillsSystem Skills = default!;
    [Dependency] protected readonly SharedAudioSystem Audio = default!;
    [Dependency] protected readonly IPrototypeManager Proto = default!;
    [Dependency] protected readonly IGameTiming Timing = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly SharedItemSystem _item = default!;

    private EntityQuery<StackComponent> _stackQuery;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WorkshopComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<WorkshopComponent, ContainerIsInsertingAttemptEvent>(OnInsertAttempt);
        SubscribeLocalEvent<WorkshopComponent, EntInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<WorkshopComponent, EntRemovedFromContainerMessage>(OnRemoved);
        SubscribeLocalEvent<WorkshopComponent, InteractUsingEvent>(OnInteractUsing, after: new[] { typeof(AnchorableSystem) });
        SubscribeLocalEvent<WorkshopComponent, BreakageEventArgs>(OnBreak);

        SubscribeLocalEvent<WorkshopComponent, AnchorStateChangedEvent>(OnAnchorChanged);

        Subs.BuiEvents<WorkshopComponent>(WorkshopUiKey.Key,
            subs =>
        {
            subs.Event<BoundUIOpenedEvent>(UpdateUserInterface);
            subs.Event<WorkshopAddToQueueMessage>(OnAddToQueue);
            subs.Event<WorkshopRemoveFromQueueMessage>(OnRemoveFromQueue);
            subs.Event<WorkshopRepeatMessage>(OnRepeat);
            subs.Event<WorkshopSuspendMessage>(OnSuspend);
        });

        _stackQuery = GetEntityQuery<StackComponent>();

        Proto.PrototypesReloaded += args =>
        {
            if (args.WasModified<WorkshopRecipePrototype>()
                || args.WasModified<WorkshopRecipeGroupPrototype>())
                ReloadPrototypes();
        };

        ReloadPrototypes();
    }

    #region Events

    private void OnInit(Entity<WorkshopComponent> ent, ref ComponentInit args)
    {
        ent.Comp.ContentStorage = Container.EnsureContainer<Container>(ent, ent.Comp.ContentContainerId);
        ent.Comp.ResultStorage = Container.EnsureContainer<Container>(ent, ent.Comp.ResultContainerId);
        UpdateAppearance(ent.Owner);
    }

    private void OnInsertAttempt(Entity<WorkshopComponent> ent, ref ContainerIsInsertingAttemptEvent args)
    {
        if (args.Container.ID == ent.Comp.ResultContainerId && ent.Comp.ResultStorage.Count >= ent.Comp.ResultCapacity)
        {
            args.Cancel();
            return;
        }

        if (args.Container.ID != ent.Comp.ContentContainerId)
            return;

        if (TryComp<ItemComponent>(args.EntityUid, out var item))
        {
            if (_item.GetSizePrototype(item.Size) > _item.GetSizePrototype(ent.Comp.MaxItemSize))
            {
                args.Cancel();
                return;
            }
        }
        else
        {
            args.Cancel();
            return;
        }

        if (ent.Comp.ContentStorage.Count >= ent.Comp.ContentCapacity)
            args.Cancel();
    }

    private void OnInserted(Entity<WorkshopComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID == ent.Comp.ContentContainerId && !TryStartCrafting(ent.AsNullable()))
            UpdateAppearance(ent.AsNullable());

        UpdateUi(ent.AsNullable());
    }

    private void OnRemoved(Entity<WorkshopComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID == ent.Comp.ContentContainerId)
            UpdateAppearance(ent.AsNullable());

        UpdateUi(ent.AsNullable());
    }

    private void OnAnchorChanged(Entity<WorkshopComponent> ent, ref AnchorStateChangedEvent args)
    {
        if (!args.Anchored)
            Container.EmptyContainer(ent.Comp.ContentStorage);
    }

    private void OnInteractUsing(Entity<WorkshopComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (TryComp<ItemComponent>(args.Used, out var item))
        {
            // check if size of an item you're trying to put in is too big
            if (_item.GetSizePrototype(item.Size) > _item.GetSizePrototype(ent.Comp.MaxItemSize))
                return;
        }
        else
            return;

        if (ent.Comp.ContentStorage.Count >= ent.Comp.ContentCapacity)
            return;

        args.Handled = true;
        _hands.TryDropIntoContainer(args.User, args.Used, ent.Comp.ContentStorage);
    }

    private void OnBreak(Entity<WorkshopComponent> ent, ref BreakageEventArgs args)
    {
        Container.EmptyContainer(ent.Comp.ContentStorage);
    }

    private void OnAddToQueue(Entity<WorkshopComponent> ent, ref WorkshopAddToQueueMessage args)
    {
        if (Ownership.HasOwner(ent.Owner, args.Actor))
            AddToQueue(ent.AsNullable(), args.ProtoId);
    }

    private void OnRemoveFromQueue(Entity<WorkshopComponent> ent, ref WorkshopRemoveFromQueueMessage args)
    {
        if (Ownership.HasOwner(ent.Owner, args.Actor))
            RemoveFromQueue(ent.AsNullable(), args.Index);
    }

    private void OnRepeat(Entity<WorkshopComponent> ent, ref WorkshopRepeatMessage args)
    {
        if (Ownership.HasOwner(ent.Owner, args.Actor))
            ToggleRepeat(ent.AsNullable(), args.Index);
    }

    private void OnSuspend(Entity<WorkshopComponent> ent, ref WorkshopSuspendMessage args)
    {
        if (Ownership.HasOwner(ent.Owner, args.Actor))
            ToggleSuspend(ent.AsNullable(), args.Index);
    }

    private void UpdateUserInterface(Entity<WorkshopComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent.AsNullable());
    }

    #endregion

    protected void AdvanceQueue(Entity<WorkshopComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        ent.Comp.Queue.SetEndTime(null);
        ent.Comp.Queue.Advance();
        DirtyField(ent, nameof(WorkshopComponent.Queue));

        if (ent.Comp.Queue.Count == 0)
        {
            RemovePassiveTask(ent.Owner);
            StopCrafting(ent);
            return;
        }

        UpdateNpcRecipe(ent.Owner);

        if (!TryStartCrafting(ent))
            StopCrafting(ent);

        UpdateAppearance(ent);
        UpdateUi(ent);
    }

    protected void UpdateAppearance(Entity<WorkshopComponent?, WorkshopVisualsComponent?, AppearanceComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp1, ref ent.Comp2, ref ent.Comp3, false))
            return;

        _appearance.SetData(ent, WorkshopVisualsState.Crafting, ent.Comp1.Crafting, ent.Comp3);
        _appearance.SetData(ent, WorkshopVisualsState.Items, ent.Comp1.ContentStorage.Count, ent.Comp3);
    }

    private TimeSpan GetCraftingEndTime(
        Entity<WorkshopComponent?> ent,
        ProtoId<WorkshopRecipePrototype> protoId)
    {
        if (!Resolve(ent, ref ent.Comp) || !Proto.Resolve(protoId, out var proto))
            return TimeSpan.Zero;

        if (!TryGetUser(ent, out var user))
            return Timing.CurTime + proto.CraftingTime * ent.Comp.CraftingTimeModifier;

        var delay = Skills.GetDelay(ent.Owner, user.Value, proto.CraftingTime);
        return Timing.CurTime + delay * ent.Comp.CraftingTimeModifier;
    }

    protected void SpawnResult(Entity<WorkshopComponent?> ent, EntProtoId protoId)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        var spawned = Spawn(protoId, Transform(ent).Coordinates);
        Container.Insert(spawned, ent.Comp.ResultStorage, force: true);

        foreach (var owner in Ownership.GetOwners(ent))
        {
            Ownership.AddOwner(spawned, owner);
        }
    }

    protected void StopCrafting(Entity<WorkshopComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        ent.Comp.Queue.SetEndTime(null);
        DirtyField(ent, nameof(WorkshopComponent.Queue));

        ent.Comp.PlayingStream = Audio.Stop(ent.Comp.PlayingStream);
        Audio.PlayPvs(ent.Comp.CraftingDoneSound, ent);
        FinishTask(ent);
        UpdateAppearance(ent);
        UpdateUi(ent);
    }

    protected void UpdateUi(Entity<WorkshopComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        _ui.SetUiState(ent.Owner,
            WorkshopUiKey.Key,
            new WorkshopUiState(
                GetNetEntityArray(ent.Comp.ContentStorage.ContainedEntities.ToArray()),
                GetNetEntityArray(ent.Comp.ResultStorage.ContainedEntities.ToArray()),
                ent.Comp.ResultCapacity,
                ent.Comp.Queue,
                ent.Comp.MaxQueue,
                GetNetEntity(ent.Comp.User),
                ent.Comp.Recipes));
    }

    #region NPC

    protected virtual void UpdateNpcRecipe(EntityUid uid) { }

    protected virtual void AddPassiveTask(Entity<WorkshopComponent?> ent) { }

    protected virtual void RemovePassiveTask(EntityUid ent) { }

    protected virtual void FinishTask(Entity<WorkshopComponent?> ent) { }

    #endregion
}
