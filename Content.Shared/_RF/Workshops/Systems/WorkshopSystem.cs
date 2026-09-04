using System.Linq;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Systems;
using Content.Shared._RF.NPC.Systems;
using Content.Shared._RF.Skills;
using Content.Shared._RF.Stockpile.Components;
using Content.Shared._RF.Stockpile.Systems;
using Content.Shared._RF.Workshops.Components;
using Content.Shared._RF.Workshops.Prototypes;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Construction.EntitySystems;
using Content.Shared.Destructible;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Content.Shared.Stacks;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._RF.Workshops.Systems;

public sealed partial class WorkshopSystem : EntitySystem
{
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private OwnershipSystem _ownership = default!;
    [Dependency] private SharedSkillsSystem _skills = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ContainerStockSupplierSystem _containerSupplier = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedPointLightSystem _pointLight = default!;
    [Dependency] private StockpileSystem _stockpile = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedStackSystem _stack = default!;
    [Dependency] private SharedItemSystem _item = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedNpcSearcherSystem _searcher = default!;

    [Dependency] private EntityQuery<StackComponent> _stackQuery = default!;

    public override void Initialize()
    {
        base.Initialize();

        Subs.ProtoReload<WorkshopRecipePrototype, WorkshopRecipeGroupPrototype>(_proto, ReloadPrototypes);
        Subs.BuiEvents<WorkshopComponent>(WorkshopUiKey.Key,
            subs =>
        {
            subs.Event<BoundUIOpenedEvent>(UpdateUserInterface);
            subs.Event<WorkshopAddToQueueMessage>(OnAddToQueue);
            subs.Event<WorkshopRemoveFromQueueMessage>(OnRemoveFromQueue);
            subs.Event<WorkshopRepeatMessage>(OnRepeat);
            subs.Event<WorkshopSuspendMessage>(OnSuspend);
            subs.Event<WorkshopSuppliedStockMessage>(OnSuppliedStock);
        });

        ReloadPrototypes();
    }

    #region Events

    [SubscribeLocalEvent]
    private void OnInit(Entity<WorkshopComponent> ent, ref ComponentInit args)
    {
        ent.Comp.ContentStorage = _container.EnsureContainer<Container>(ent, WorkshopComponent.ContentContainerId);
        ent.Comp.ResultStorage = _container.EnsureContainer<Container>(ent, WorkshopComponent.ResultContainerId);
        UpdateAppearance(ent.Owner);
    }

    [SubscribeLocalEvent]
    private void OnInsertAttempt(Entity<WorkshopComponent> ent, ref ContainerIsInsertingAttemptEvent args)
    {
        if (args.Container.ID == WorkshopComponent.ResultContainerId
            && ent.Comp.ResultStorage.Count >= ent.Comp.ResultCapacity)
        {
            args.Cancel();
            return;
        }

        if (args.Container.ID != WorkshopComponent.ContentContainerId)
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

    [SubscribeLocalEvent]
    private void OnInserted(Entity<WorkshopComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID == WorkshopComponent.ContentContainerId)
        {
            var ev = new WorkshopIngredientInserted(ent, args.Entity);
            RaiseLocalEvent(ent, ref ev, true);
            RaiseLocalEvent(args.Entity, ref ev, true);

            TryStartCrafting(ent.AsNullable());
        }

        UpdateAppearance(ent.AsNullable());
        UpdateUi(ent.AsNullable());
    }

    [SubscribeLocalEvent]
    private void OnRemoved(Entity<WorkshopComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID == WorkshopComponent.ContentContainerId)
        {
            if (ent.Comp.CraftingIngredients.Contains(args.Entity))
                StopCrafting(ent.AsNullable());

            var ev = new WorkshopIngredientRemoved(ent, args.Entity);
            RaiseLocalEvent(ent, ref ev, true);
            RaiseLocalEvent(args.Entity, ref ev, true);

            UpdateAppearance(ent.AsNullable());
        }

        UpdateUi(ent.AsNullable());
    }

    [SubscribeLocalEvent]
    private void OnAnchorChanged(Entity<WorkshopComponent> ent, ref AnchorStateChangedEvent args)
    {
        if (!TerminatingOrDeleted(ent) && !args.Anchored)
            _container.EmptyContainer(ent.Comp.ContentStorage);
    }

    [SubscribeLocalEvent]
    private void OnInteractHand(Entity<WorkshopComponent> ent, ref InteractHandEvent args)
    {
        TryStartCrafting(ent.AsNullable());
    }

    [SubscribeLocalEvent(after: new[] { typeof(AnchorableSystem) })]
    private void OnInteractUsing(Entity<WorkshopComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<ItemComponent>(args.Used, out var item))
            return;

        // check if size of an item you're trying to put in is too big
        if (_item.GetSizePrototype(item.Size) > _item.GetSizePrototype(ent.Comp.MaxItemSize))
            return;

        if (ent.Comp.ContentStorage.Count >= ent.Comp.ContentCapacity)
            return;

        args.Handled = true;
        _hands.TryDropIntoContainer(args.User, args.Used, ent.Comp.ContentStorage);
    }

    [SubscribeLocalEvent]
    private void OnBreak(Entity<WorkshopComponent> ent, ref BreakageEventArgs args)
    {
        _container.EmptyContainer(ent.Comp.ContentStorage);
    }

    [SubscribeLocalEvent]
    private void OnCaptured(Entity<WorkshopComponent> ent, ref SearchResultCaptured args)
    {
        ent.Comp.User = args.User;
        var workshop = ent.AsNullable();

        // We capture the ingredients so that other AIs don't try to take them while we're crafting.
        if (_proto.TryIndex(GetCurrentRecipe(workshop), out var proto))
        {
            GetIngredientsEntities(workshop, proto, out var entities);
            _searcher.CaptureResult(entities, args.User);
        }

        UpdateUi(workshop);
    }

    [SubscribeLocalEvent]
    private void OnReleased(Entity<WorkshopComponent> ent, ref SearchResultReleased args)
    {
        ent.Comp.User = null;
        _searcher.ReleaseCapturedResult(ent.Comp.ContentStorage.ContainedEntities, args.User);
        UpdateUi(ent.AsNullable());
    }

    [SubscribeLocalEvent]
    private void OnWorkshopDoAfter(Entity<WorkshopComponent> ent, ref WorkshopCraftingDoAfterEvent args)
    {
        var workshop = ent.AsNullable();

        if (args.Cancelled
            || !TryGetUser(workshop, out var user)
            || !_proto.Resolve(GetCurrentRecipe(workshop), out var proto))
        {
            StopCrafting(workshop);
            return;
        }

        foreach (var exp in proto.SkillsUp)
        {
            _skills.AddExperience(user.Value, exp);
        }

        DeleteIngredients(ent.Comp, proto);

        if (_skills.DoInteractionCheck(ent.Owner, user.Value) != SkillCheckResult.Fail)
        {
            SpawnResult(workshop, proto.Result);
            ent.Comp.CraftingDoAfter = null;
            AdvanceQueue(workshop);
            return;
        }

        if (ent.Comp.CraftingFailResult != null)
            SpawnResult(workshop, ent.Comp.CraftingFailResult.Value);

        _audio.PlayPvs(ent.Comp.CraftingFailSound, workshop);

        ent.Comp.CraftingDoAfter = null;

        if (!TryStartCrafting(workshop))
            StopCrafting(workshop);
        else
            AdvanceQueue(workshop);
    }

    private void OnAddToQueue(Entity<WorkshopComponent> ent, ref WorkshopAddToQueueMessage args)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        if (_ownership.HasOwner(ent.Owner, args.Actor))
            AddToQueue(ent.AsNullable(), args.ProtoId);
    }

    private void OnRemoveFromQueue(Entity<WorkshopComponent> ent, ref WorkshopRemoveFromQueueMessage args)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        if (_ownership.HasOwner(ent.Owner, args.Actor))
            RemoveFromQueue(ent.AsNullable(), args.Index);
    }

    private void OnRepeat(Entity<WorkshopComponent> ent, ref WorkshopRepeatMessage args)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        if (_ownership.HasOwner(ent.Owner, args.Actor))
            ToggleRepeat(ent.AsNullable(), args.Index);
    }

    private void OnSuspend(Entity<WorkshopComponent> ent, ref WorkshopSuspendMessage args)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        if (_ownership.HasOwner(ent.Owner, args.Actor))
            ToggleSuspend(ent.AsNullable(), args.Index);
    }

    private void OnSuppliedStock(Entity<WorkshopComponent> ent, ref WorkshopSuppliedStockMessage args)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        if (!_ownership.HasOwner(ent.Owner, args.Actor)
            || !TryComp(ent, out ContainerStockSupplierComponent? comp))
            return;

        if (_stockpile.TryGetStock(args.StockId, out var stock)
            && _ownership.HasOwner(stock.Value.Owner, args.Actor))
            _containerSupplier.SetOnlySupplied(new(ent, comp), stock.Value);
        else
            _containerSupplier.ClearSupplied(new(ent, comp));
    }

    private void UpdateUserInterface(Entity<WorkshopComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (_ownership.HasOwner(ent.Owner, args.Actor))
            UpdateUi(ent.AsNullable());
    }

    #endregion

    private void AdvanceQueue(Entity<WorkshopComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        ent.Comp.Queue.SetEndTime(null);
        ent.Comp.Queue.Advance();
        DirtyField(ent, nameof(WorkshopComponent.Queue));

        if (ent.Comp.Queue.Count == 0 || ent.Comp.Queue.Queue.All(x => x.Suspended))
        {
            StopCrafting(ent);
            return;
        }

        if (!TryStartCrafting(ent))
            StopCrafting(ent);

        UpdateAppearance(ent);
        UpdateUi(ent);
    }

    private void UpdateAppearance(Entity<WorkshopComponent?, WorkshopVisualsComponent?, AppearanceComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp1, ref ent.Comp2, ref ent.Comp3, false))
            return;

        _appearance.SetData(ent, WorkshopVisualsState.Crafting, ent.Comp1.Crafting, ent.Comp3);
        _appearance.SetData(ent, WorkshopVisualsState.Items, ent.Comp1.ContentStorage.Count, ent.Comp3);
    }

    private void UpdateAudioLoop(Entity<WorkshopComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        if (ent.Comp.Crafting && ent.Comp.PlayingStream == null)
        {
            var param = ent.Comp.LoopingSound?.Params.WithLoop(true) ?? AudioParams.Default.WithLoop(true);
            ent.Comp.PlayingStream = _audio.PlayPvs(ent.Comp.LoopingSound, ent, param)?.Entity;
        }
        else if (!ent.Comp.Crafting)
            ent.Comp.PlayingStream = _audio.Stop(ent.Comp.PlayingStream);
    }

    private void UpdateLight(Entity<WorkshopComponent?> ent)
    {
        if (Resolve(ent, ref ent.Comp) && _pointLight.TryGetLight(ent, out var light))
            _pointLight.SetEnabled(ent, ent.Comp.Crafting, light);
    }

    private void UpdateUi(Entity<WorkshopComponent?> ent)
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
                GetNetEntity(GetUser(ent)),
                ent.Comp.Recipes));
    }
}
