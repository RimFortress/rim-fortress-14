using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared._RF.NPC;
using Content.Shared._RF.Skills;
using Content.Shared._RF.Workshops.Components;
using Content.Shared._RF.Workshops.Prototypes;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Construction.EntitySystems;
using Content.Shared.Destructible;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Content.Shared.Stacks;
using JetBrains.Annotations;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Shared._RF.Workshops.Systems;

public abstract class SharedWorkshopSystem : EntitySystem
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

    private FrozenDictionary<EntProtoId, List<ProtoId<WorkshopRecipePrototype>>> _recipes
        = new Dictionary<EntProtoId, List<ProtoId<WorkshopRecipePrototype>>>().ToFrozenDictionary();

    private FrozenDictionary<ProtoId<WorkshopRecipeGroupPrototype>, HashSet<ProtoId<WorkshopRecipeGroupPrototype>>>
        _groupsParents
            = new Dictionary<ProtoId<WorkshopRecipeGroupPrototype>, HashSet<ProtoId<WorkshopRecipeGroupPrototype>>>()
                .ToFrozenDictionary();

    private FrozenDictionary<ProtoId<WorkshopRecipeGroupPrototype>, HashSet<ProtoId<WorkshopRecipePrototype>>>
        _groupRecipes
            = new Dictionary<ProtoId<WorkshopRecipeGroupPrototype>, HashSet<ProtoId<WorkshopRecipePrototype>>>()
                .ToFrozenDictionary();

    private FrozenSet<ProtoId<WorkshopRecipeGroupPrototype>> _nullGroups
        = new HashSet<ProtoId<WorkshopRecipeGroupPrototype>>().ToFrozenSet();
    private FrozenSet<ProtoId<WorkshopRecipePrototype>> _nullRecipes
        = new HashSet<ProtoId<WorkshopRecipePrototype>>().ToFrozenSet();

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

    private void ReloadPrototypes()
    {
        var recipes = new Dictionary<EntProtoId, List<ProtoId<WorkshopRecipePrototype>>>();
        var groupsParents = new Dictionary<ProtoId<WorkshopRecipeGroupPrototype>, HashSet<ProtoId<WorkshopRecipeGroupPrototype>>>();
        var groupRecipes = new Dictionary<ProtoId<WorkshopRecipeGroupPrototype>, HashSet<ProtoId<WorkshopRecipePrototype>>>();
        var nullGroups = new HashSet<ProtoId<WorkshopRecipeGroupPrototype>>();
        var nullRecipes = new HashSet<ProtoId<WorkshopRecipePrototype>>();

        foreach (var proto in Proto.EnumeratePrototypes<WorkshopRecipePrototype>())
        {
            if (!recipes.ContainsKey(proto.Result))
                recipes[proto.Result] = new();

            recipes[proto.Result].Add(proto);

            if (proto.Group == null)
            {
                nullRecipes.Add(proto);
                continue;
            }

            if (!groupRecipes.TryAdd(proto.Group.Value, new() { proto }))
                groupRecipes[proto.Group.Value].Add(proto);
        }

        foreach (var proto in Proto.EnumeratePrototypes<WorkshopRecipeGroupPrototype>())
        {
            groupsParents[proto] = proto.SubGroups.ToHashSet();
        }

        foreach (var (group, _) in groupsParents)
        {
            var @null = true;

            foreach (var (protoId, list) in groupsParents)
            {
                if (group == protoId || !list.Contains(group))
                    continue;

                @null = false;
                break;
            }

            if (@null)
                nullGroups.Add(group);
        }

        _recipes = recipes.ToFrozenDictionary();
        _groupsParents = groupsParents.ToFrozenDictionary();
        _groupRecipes = groupRecipes.ToFrozenDictionary();
        _nullGroups = nullGroups.ToFrozenSet();
        _nullRecipes = nullRecipes.ToFrozenSet();
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

    private void UpdateUserInterface(Entity<WorkshopComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent.AsNullable());
    }

    #endregion

    protected void DeleteIngredients(WorkshopComponent comp, WorkshopRecipePrototype proto)
    {
        var ingredients = new WorkshopRecipeIngredients(proto.Ingredients);

        foreach (var uid in comp.ContentStorage.ContainedEntities)
        {
            if (_stackQuery.TryComp(uid, out var stack)
                && ingredients.Materials.TryGetValue(stack.StackTypeId, out var stackCount))
            {
                if (stack.Count == stackCount)
                {
                    QueueDel(uid);
                    ingredients.Materials.Remove(stack.StackTypeId);
                    continue;
                }

                if (stack.Count > stackCount)
                {
                    _stack.SetCount(uid, stack.Count - stackCount, stack);
                    ingredients.Materials.Remove(stack.StackTypeId);
                }
                else
                {
                    QueueDel(uid);
                    ingredients.Materials[stack.StackTypeId] -= stack.Count;
                    continue;
                }
            }

            if (Prototype(uid) is { } entProto
                && ingredients.Items.TryGetValue(entProto, out var itemCount))
            {
                QueueDel(uid);

                if (itemCount <= 1)
                    ingredients.Items.Remove(entProto);
                else
                    ingredients.Items[entProto]--;

                continue;
            }

            if (!Solution.TryGetDrainableSolution(uid, out var solutionEntity, out var solution))
                continue;

            foreach (var (reagent, _) in ingredients.Reagents)
            {
                // removed everything
                if (!ingredients.Reagents.TryGetValue(reagent, out var reagentCount))
                    continue;

                var quant = solution.GetTotalPrototypeQuantity(reagent);

                if (quant >= reagentCount)
                {
                    quant = reagentCount;
                    ingredients.Reagents.Remove(reagent);
                }
                else
                    ingredients.Reagents[reagent] -= quant;

                Solution.RemoveReagent(solutionEntity.Value, reagent, quant);
            }
        }
    }

    private void GetIngredients(
        Entity<WorkshopComponent?> ent,
        out WorkshopRecipeIngredients ingredients)
    {
        ingredients = new();

        if (!Resolve(ent, ref ent.Comp))
            return;

        GetIngredients(ent.Comp, out ingredients.Materials, out ingredients.Items, out ingredients.Reagents);
    }

    private void GetIngredients(
        WorkshopComponent comp,
        out Dictionary<ProtoId<StackPrototype>, int> materials,
        out Dictionary<EntProtoId, int> items,
        out Dictionary<ProtoId<ReagentPrototype>, FixedPoint2> reagents)
    {
        materials = new();
        items = new();
        reagents = new();

        foreach (var uid in comp.ContentStorage.ContainedEntities)
        {
            if (EntityManager.IsQueuedForDeletion(uid))
                continue;

            if (Prototype(uid) is { } proto && !items.TryAdd(proto, 1))
                items[proto]++;

            if (_stackQuery.TryComp(uid, out var stack) && !materials.TryAdd(stack.StackTypeId, stack.Count))
                materials[stack.StackTypeId] += stack.Count;

            if (!Solution.TryGetDrainableSolution(uid, out _, out var sol))
                continue;

            foreach (var (reagent, quantity) in sol)
            {
                if (!reagents.TryAdd(reagent.Prototype, quantity))
                    reagents[reagent.Prototype] += quantity;
            }
        }
    }

    protected WorkshopRecipeIngredients GetRemainingIngredients(
        WorkshopComponent comp,
        ProtoId<WorkshopRecipePrototype> protoId)
    {
        if (!Proto.Resolve(protoId, out var proto))
            return new();

        var ingredients = new WorkshopRecipeIngredients(proto.Ingredients);

        foreach (var uid in comp.ContentStorage.ContainedEntities)
        {
            if (EntityManager.IsQueuedForDeletion(uid))
                continue;

            if (_stackQuery.TryComp(uid, out var stack)
                && ingredients.Materials.TryGetValue(stack.StackTypeId, out var stackCount))
            {
                if (stack.Count == stackCount)
                {
                    ingredients.Materials.Remove(stack.StackTypeId);
                    continue;
                }

                if (stack.Count > stackCount)
                    ingredients.Materials.Remove(stack.StackTypeId);
                else
                {
                    ingredients.Materials[stack.StackTypeId] -= stack.Count;
                    continue;
                }
            }

            if (Prototype(uid) is { } ent && ingredients.Items.TryGetValue(ent, out var count))
            {
                if (count <= 1)
                    ingredients.Items.Remove(ent);
                else
                    ingredients.Items[ent]--;
            }

            if (!Solution.TryGetDrainableSolution(uid, out _, out var solution))
                continue;

            foreach (var (reagent, _) in ingredients.Reagents)
            {
                // removed everything
                if (!ingredients.Reagents.TryGetValue(reagent, out var reagentCount))
                    continue;

                var quant = solution.GetTotalPrototypeQuantity(reagent);

                if (quant >= reagentCount)
                    ingredients.Reagents.Remove(reagent);
                else
                    ingredients.Reagents[reagent] -= quant;
            }
        }

        return ingredients;
    }

    protected void AdvanceQueue(Entity<WorkshopComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        ent.Comp.CraftEndTime = null;
        ent.Comp.CraftStartTime = null;

        if (ent.Comp.Queue.Count == 0)
        {
            StopCrafting(ent);
            return;
        }

        var head = ent.Comp.Queue[0];

        if (head.Pathfinding.Length > 0)
        {
            head = head.Advance();
            ent.Comp.Queue[0] = head;
        }
        else
            ent.Comp.Queue.RemoveAt(0);

        Dirty(ent);

        if (!TryStartCrafting(ent))
        {
            StopCrafting(ent);
            return;
        }

        UpdateNpcRecipe(ent);
        UpdateAppearance(ent);
        UpdateUi(ent);
    }

    protected void UpdateAppearance(Entity<WorkshopComponent?, WorkshopVisualsComponent?, AppearanceComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp1, ref ent.Comp2, ref ent.Comp3, false)
            || ent.Comp2.ItemsVisualStates.Count == 0)
            return;

        var state = ent.Comp2.ItemsVisualStates
            .Where(x => x.Value >= ent.Comp1.ContentStorage.Count)
            .OrderBy(x => x.Value)
            .FirstOrDefault()
            .Key ?? ent.Comp2.ItemsVisualStates.First().Key;

        _appearance.SetData(ent, WorkshopVisuals.Items, state, ent.Comp3);
        _appearance.SetData(
            ent,
            WorkshopVisuals.Crafting,
            ent.Comp1.CraftEndTime == null ? ent.Comp2.IdleState : state,
            ent.Comp3);
    }

    private TimeSpan GetCraftingEndTime(
        Entity<WorkshopComponent?> ent,
        ProtoId<WorkshopRecipePrototype> protoId)
    {
        if (!Resolve(ent, ref ent.Comp) || !Proto.Resolve(protoId, out var proto))
            return TimeSpan.Zero;

        if (!TryGetUser(ent, out var user))
            return Timing.CurTime + proto.CraftingTime;

        return Timing.CurTime + Skills.GetDelay(ent.Owner, user.Value, proto.CraftingTime);
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

        ent.Comp.CraftEndTime = null;
        ent.Comp.CraftStartTime = null;
        ent.Comp.PlayingStream = Audio.Stop(ent.Comp.PlayingStream);
        Audio.PlayPvs(ent.Comp.CraftingDoneSound, ent);
        FinishTask(ent);
        UpdateAppearance(ent);
        UpdateUi(ent);
        Dirty(ent);
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
                ent.Comp.CraftEndTime,
                ent.Comp.CraftStartTime,
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

    #region API

    /// <summary>
    /// Checks if all the ingredients for the target recipe are available in the workshop.
    /// </summary>
    [PublicAPI, Pure]
    public bool CanCraft(Entity<WorkshopComponent?> ent, ProtoId<WorkshopRecipePrototype> protoId)
    {
        if (!Resolve(ent, ref ent.Comp)
            || !Proto.Resolve(protoId, out var proto)
            || !ContainsRecipe(ent, protoId))
            return false;

        GetIngredients(ent, out var ingredients);
        return proto.Ingredients.Satisfied(ingredients);
    }

    /// <summary>
    /// Returns the recipe from the workshop queue with the specified index.
    /// </summary>
    [PublicAPI, Pure]
    public static ProtoId<WorkshopRecipePrototype>? GetQueueRecipe(WorkshopComponent comp, int index)
    {
        if (index < 0 || index >= comp.Queue.Count)
            return null;

        return comp.Queue[index].Current;
    }

    /// <summary>
    /// Returns the first recipe from the workshop queue.
    /// </summary>
    [PublicAPI, Pure]
    public ProtoId<WorkshopRecipePrototype>? GetCurrentRecipe(Entity<WorkshopComponent?> ent)
        => !Resolve(ent, ref ent.Comp) ? null : GetQueueRecipe(ent.Comp, 0);

    /// <summary>
    /// Builds a path from recipes to create the target recipe.
    /// </summary>
    /// <param name="protoId">Target recipe.</param>
    /// <param name="tableId">Available recipes table.</param>
    /// <returns>A recipe path.</returns>
    [PublicAPI, Pure]
    public List<ProtoId<WorkshopRecipePrototype>> GetRecipePath(
        ProtoId<WorkshopRecipePrototype> protoId,
        ProtoId<WorkshopRecipeTablePrototype> tableId)
    {
        var path = new List<ProtoId<WorkshopRecipePrototype>>();

        var recipes = new List<Dictionary<ProtoId<WorkshopRecipePrototype>, int>>();
        var queue = new Queue<(int Depth, ProtoId<WorkshopRecipePrototype> Proto, int Quantity)>();
        var tableRecipes = GetTableRecipes(tableId);

        queue.Enqueue((0, protoId, 1));

        while (queue.TryDequeue(out var recipe))
        {
            if (!Proto.Resolve(recipe.Proto, out var proto))
                continue;

            if (recipes.Count <= recipe.Depth)
                recipes.Add(new());

            // If several identical ingredients are used at the same depth, we add their quantities together
            if (!recipes[recipe.Depth].TryAdd(recipe.Proto, recipe.Quantity))
                recipes[recipe.Depth][recipe.Proto] += recipe.Quantity;

            foreach (var (ent, q) in proto.Ingredients.Items)
            {
                // Else, search for a recipe for the ingredient
                if (!_recipes.TryGetValue(ent, out var list))
                    continue;

                if (list.FirstOrNull(tableRecipes.Contains) is { } valid)
                    queue.Enqueue((recipe.Depth + 1, valid, q * recipe.Quantity));
            }
        }

        recipes.RemoveAt(0);

        foreach (var depth in recipes)
        {
            foreach (var (proto, quan) in depth)
            {
                for (var i = 0; i < quan; i++)
                {
                    path.Add(proto);
                }
            }
        }

        path.Reverse();
        return path;
    }

    /// <summary>
    /// Starts crafting the first item in the workshop queue, if all the required items are available.
    /// </summary>
    /// <param name="ent">Workshop entity.</param>
    /// <returns>True, if the creating has been successfully started.</returns>
    [PublicAPI]
    public bool TryStartCrafting(Entity<WorkshopComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp)
            || ent.Comp.CraftEndTime != null
            || ent.Comp.ResultStorage.Count >= ent.Comp.ResultCapacity
            || GetCurrentRecipe(ent) is not { } protoId
            || !CanCraft(ent, protoId))
            return false;

        Audio.PlayPvs(ent.Comp.StartCraftingSound, ent);
        ent.Comp.PlayingStream =
            Audio.PlayPvs(ent.Comp.LoopingSound, ent, AudioParams.Default.WithLoop(true).WithMaxDistance(5))?.Entity;
        ent.Comp.CraftEndTime = GetCraftingEndTime(ent, protoId);
        ent.Comp.CraftStartTime = Timing.CurTime;
        DirtyField(ent, nameof(WorkshopComponent.CraftEndTime));
        DirtyField(ent, nameof(WorkshopComponent.CraftStartTime));
        UpdateAppearance(ent);
        UpdateUi(ent);
        return true;
    }

    /// <summary>
    /// Adds the recipe to the workshop crafting queue.
    /// </summary>
    /// <param name="ent">Workshop entity.</param>
    /// <param name="protoId">Recipe prototype.</param>
    /// <returns>True, if the recipe was successfully added.</returns>
    [PublicAPI]
    public bool AddToQueue(Entity<WorkshopComponent?> ent, ProtoId<WorkshopRecipePrototype> protoId)
    {
        if (!Resolve(ent, ref ent.Comp)
            || ent.Comp.Queue.Count >= ent.Comp.MaxQueue
            || !ContainsRecipe(ent, protoId))
            return false;

        ent.Comp.Queue.Add(new WorkshopQueueEntry(protoId,
            GetRecipePath(protoId, ent.Comp.Recipes).ToArray()));
        Dirty(ent);

        AddPassiveTask(ent);
        UpdateAppearance(ent);
        UpdateUi(ent);
        return true;
    }

    /// <summary>
    /// Removes the recipe from the workshop's crafting queue.
    /// </summary>
    /// <param name="ent">Workshop entity.</param>
    /// <param name="index">Recipe index in the crafting queue.</param>
    /// <returns>True, if the recipe was successfully removed.</returns>
    [PublicAPI]
    public bool RemoveFromQueue(Entity<WorkshopComponent?> ent, int index)
    {
        if (!Resolve(ent, ref ent.Comp)
            || index < 0
            || index >= ent.Comp.Queue.Count)
            return false;

        ent.Comp.Queue.RemoveAt(index);
        Dirty(ent);

        if (ent.Comp.Queue.Count == 0)
        {
            StopCrafting(ent);
            RemovePassiveTask(ent);
            return true;
        }

        // Remove active recipe
        if (index == 0)
        {
            ent.Comp.CraftEndTime = null;
            ent.Comp.CraftStartTime = null;
            ent.Comp.PlayingStream = Audio.Stop(ent.Comp.PlayingStream);

            if (!TryStartCrafting(ent))
                StopCrafting(ent);

            UpdateNpcRecipe(ent);
            return true;
        }

        UpdateAppearance(ent);
        UpdateUi(ent);
        return true;
    }

    /// <summary>
    /// Returns all the ingredients needed to create the recipe.
    /// If the ingredients are recipes themselves,
    /// the ingredients for those recipes will also be included in the returned list.
    /// </summary>
    [PublicAPI, Pure]
    public WorkshopRecipeIngredients GetRecipeIngredients(
        ProtoId<WorkshopRecipePrototype> protoId,
        ProtoId<WorkshopRecipeTablePrototype> tableId)
    {
        if (!Proto.Resolve(protoId, out var proto))
            return new();

        var ingredients = new WorkshopRecipeIngredients(proto.Ingredients);
        var path = GetRecipePath(protoId, tableId);

        foreach (var recipeId in path)
        {
            if (!Proto.Resolve(recipeId, out var recipe))
                continue;

            if (ingredients.Items.TryGetValue(recipe.Result, out var count))
            {
                if (count == 1)
                    ingredients.Items.Remove(recipe.Result);
                else
                    ingredients.Items[recipe.Result]--;
            }

            ingredients = ingredients.UnionWith(recipe.Ingredients);
        }

        return ingredients;
    }

    /// <summary>
    /// Returns current workshop NPC user.
    /// </summary>
    [PublicAPI]
    public virtual bool TryGetUser(Entity<WorkshopComponent?> ent, [NotNullWhen(true)] out EntityUid? user)
    {
        user = null;
        return false;
    }

    /// <summary>
    /// Returns current workshop NPC user.
    /// </summary>
    [PublicAPI]
    public virtual EntityUid? GetUser(Entity<WorkshopComponent?> ent)
    {
        return null;
    }

    /// <summary>
    /// Returns the parent group for given, if any.
    /// </summary>
    [PublicAPI, Pure]
    public ProtoId<WorkshopRecipeGroupPrototype>? GetParentGroup(ProtoId<WorkshopRecipeGroupPrototype>? protoId)
    {
        if (protoId == null)
            return null;

        foreach (var (group, children) in _groupsParents)
        {
            if (children.Contains(protoId.Value))
                return group;
        }

        return null;
    }

    /// <summary>
    /// Checks whether the recipe is in the workshop recipe table.
    /// </summary>
    [PublicAPI, Pure]
    public bool ContainsRecipe(Entity<WorkshopComponent?> ent, ProtoId<WorkshopRecipePrototype> recipe)
        => Resolve(ent, ref ent.Comp, false) && ContainsRecipe(ent.Comp.Recipes, recipe);

    /// <summary>
    /// Checks whether the recipe is in the recipe table.
    /// </summary>
    [PublicAPI, Pure]
    public bool ContainsRecipe(ProtoId<WorkshopRecipeTablePrototype> protoId, ProtoId<WorkshopRecipePrototype> recipe)
    {
        if (!Proto.Resolve(protoId, out var proto))
            return false;

        if (proto.Recipes.Contains(recipe))
            return true;

        foreach (var subTable in proto.Tables)
        {
            if (ContainsRecipe(subTable, recipe))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Checks whether the table contains a recipe belonging to a given group.
    /// </summary>
    [PublicAPI, Pure]
    public bool ContainsGroup(ProtoId<WorkshopRecipeTablePrototype> protoId, ProtoId<WorkshopRecipeGroupPrototype> group)
    {
        if (!Proto.Resolve(protoId, out var proto))
            return false;

        foreach (var recipeId in proto.Recipes)
        {
            if (Proto.Resolve(recipeId, out var recipe) && recipe.Group == group)
                return true;
        }

        foreach (var subTable in proto.Tables)
        {
            if (ContainsGroup(subTable, group))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Returns all recipes from the recipes table.
    /// </summary>
    [PublicAPI, Pure]
    public HashSet<ProtoId<WorkshopRecipePrototype>> GetTableRecipes(ProtoId<WorkshopRecipeTablePrototype> tableId)
    {
        if (!Proto.Resolve(tableId, out var proto))
            return new();

        var tableRecipes = new HashSet<ProtoId<WorkshopRecipePrototype>>();

        foreach (var recipe in proto.Recipes)
        {
            tableRecipes.Add(recipe);
        }

        foreach (var table in proto.Tables)
        {
            tableRecipes.UnionWith(GetTableRecipes(table));
        }

        return tableRecipes;
    }

    /// <summary>
    /// Returns all recipe groups from the recipes table.
    /// </summary>
    [PublicAPI, Pure]
    public HashSet<ProtoId<WorkshopRecipeGroupPrototype>> GetTableGroups(ProtoId<WorkshopRecipeTablePrototype> tableId)
    {
        if (!Proto.Resolve(tableId, out var proto))
            return new();

        var tableGroups = new HashSet<ProtoId<WorkshopRecipeGroupPrototype>>();

        foreach (var recipeId in proto.Recipes)
        {
            if (Proto.Resolve(recipeId, out var recipe) && recipe.Group != null)
                tableGroups.Add(recipe.Group.Value);
        }

        foreach (var table in proto.Tables)
        {
            tableGroups.UnionWith(GetTableGroups(table));
        }

        return tableGroups;
    }

    /// <summary>
    /// Returns the parent group for given, if any.
    /// </summary>
    [PublicAPI, Pure]
    public ProtoId<WorkshopRecipeGroupPrototype>? GetParentGroup(
        ProtoId<WorkshopRecipeGroupPrototype>? protoId,
        ProtoId<WorkshopRecipeTablePrototype> tableId)
    {
        if (protoId == null)
            return null;

        var groups = GetTableGroups(tableId);

        foreach (var (group, children) in _groupsParents)
        {
            if (children.Contains(protoId.Value) && groups.Contains(group))
                return group;
        }

        return null;
    }

    [PublicAPI, Pure]
    public HashSet<ProtoId<WorkshopRecipePrototype>> GetGroupRecipes(
        ProtoId<WorkshopRecipeGroupPrototype>? protoId,
        HashSet<ProtoId<WorkshopRecipePrototype>> recipes)
    {
        if (protoId == null || !_groupRecipes.TryGetValue(protoId.Value, out var groupRecipes))
            groupRecipes = _nullRecipes.ToHashSet();

        var tableRecipes = new HashSet<ProtoId<WorkshopRecipePrototype>>();

        foreach (var recipe in recipes)
        {
            if (groupRecipes.Contains(recipe))
                tableRecipes.Add(recipe);
        }

        return tableRecipes;
    }

    [PublicAPI, Pure]
    public HashSet<ProtoId<WorkshopRecipeGroupPrototype>> GetChildrenGroups(
        ProtoId<WorkshopRecipeGroupPrototype>? protoId,
        ProtoId<WorkshopRecipeTablePrototype> tableId)
    {
        if (protoId == null || !_groupsParents.TryGetValue(protoId.Value, out var children))
            children = _nullGroups.ToHashSet();

        var groups = new HashSet<ProtoId<WorkshopRecipeGroupPrototype>>();

        foreach (var group in GetTableGroups(tableId))
        {
            if (protoId != group && children.Contains(group))
                groups.Add(group);
        }

        return groups;
    }

    [PublicAPI, Pure]
    public int GetGroupCount(
        ProtoId<WorkshopRecipeGroupPrototype> protoId,
        HashSet<ProtoId<WorkshopRecipePrototype>> recipes)
        => !_groupRecipes.TryGetValue(protoId, out var groupRecipes) ? 0 : groupRecipes.Count(recipes.Contains);

    #endregion
}
