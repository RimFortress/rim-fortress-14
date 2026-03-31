using System.Linq;
using Content.Server._RF.NPC.Components;
using Content.Server._RF.NPC.Systems;
using Content.Server._RF.Skills;
using Content.Server._RF.Workshops.Components;
using Content.Server.Hands.Systems;
using Content.Server.Item;
using Content.Server.NPC.HTN;
using Content.Server.Popups;
using Content.Shared._RF.NPC;
using Content.Shared._RF.Workshops.Components;
using Content.Shared._RF.Workshops.Prototypes;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Construction.EntitySystems;
using Content.Shared.Destructible;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Stacks;
using JetBrains.Annotations;
using Robust.Server.Audio;
using Robust.Server.Containers;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._RF.Workshops.Systems;

public sealed partial class WorkshopSystem : EntitySystem
{
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly NpcControlSystem _npcControl = default!;
    [Dependency] private readonly ContainerSystem _container = default!;
    [Dependency] private readonly OwnershipSystem _ownership = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SkillsSystem _skills = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly HandsSystem _hands = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly ItemSystem _item = default!;

    private EntityQuery<StackComponent> _stackQuery;

    private readonly Dictionary<EntProtoId, List<ProtoId<WorkshopRecipePrototype>>> _recipes = new();

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
        SubscribeLocalEvent<WorkshopComponent, NpcTaskGivenTarget>(OnTaskGiven);
        SubscribeLocalEvent<WorkshopComponent, NpcTaskFinishedTarget>(OnTaskFinished);

        SubscribeLocalEvent<WorkshopComponent, WorkshopAddToQueueMessage>(OnAddToQueue);
        SubscribeLocalEvent<WorkshopComponent, WorkshopRemoveFromQueueMessage>(OnRemoveFromQueue);

        _stackQuery = GetEntityQuery<StackComponent>();

        _proto.PrototypesReloaded += args =>
        {
            if (args.WasModified<WorkshopRecipePrototype>())
                ReloadPrototypes();
        };

        ReloadPrototypes();
    }

    private void ReloadPrototypes()
    {
        _recipes.Clear();

        foreach (var proto in _proto.EnumeratePrototypes<WorkshopRecipePrototype>())
        {
            if (!_recipes.ContainsKey(proto.Result))
                _recipes[proto.Result] = new();

            _recipes[proto.Result].Add(proto);
        }
    }

    #region Events

    private void OnInit(Entity<WorkshopComponent> ent, ref ComponentInit args)
    {
        ent.Comp.Storage = _container.EnsureContainer<Container>(ent, ent.Comp.ContainerId);
        UpdateAppearance(ent.Owner);
    }

    private void OnInsertAttempt(Entity<WorkshopComponent> ent, ref ContainerIsInsertingAttemptEvent args)
    {
        if (args.Container.ID != ent.Comp.ContainerId)
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

        if (ent.Comp.Storage.Count >= ent.Comp.Capacity)
            args.Cancel();
    }

    private void OnInserted(Entity<WorkshopComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (!TryStartCrafting(ent.AsNullable()))
            UpdateAppearance(ent.AsNullable());
    }

    private void OnRemoved(Entity<WorkshopComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        UpdateAppearance(ent.AsNullable());
    }

    private void OnAnchorChanged(Entity<WorkshopComponent> ent, ref AnchorStateChangedEvent args)
    {
        if (!args.Anchored)
            _container.EmptyContainer(ent.Comp.Storage);
    }

    private void OnTaskGiven(EntityUid uid, WorkshopComponent component, NpcTaskGivenTarget args)
    {
        component.User = args.User;
        UpdateUi(uid);
    }

    private void OnTaskFinished(EntityUid uid, WorkshopComponent component, NpcTaskFinishedTarget args)
    {
        component.User = null;
        UpdateUi(uid);
    }

    private void OnInteractUsing(Entity<WorkshopComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (TryComp<ItemComponent>(args.Used, out var item))
        {
            // check if size of an item you're trying to put in is too big
            if (_item.GetSizePrototype(item.Size) > _item.GetSizePrototype(ent.Comp.MaxItemSize))
            {
                _popup.PopupEntity(
                    Loc.GetString("workshop-component-interact-item-too-big", ("item", args.Used)),
                    ent,
                    args.User);
                return;
            }
        }
        else
        {
            // check if thing you're trying to put in isn't an item
            _popup.PopupEntity(Loc.GetString("workshop-component-interact-using-transfer-fail"),
                ent,
                args.User);
            return;
        }

        if (ent.Comp.Storage.Count >= ent.Comp.Capacity)
        {
            _popup.PopupEntity(Loc.GetString("workshop-component-interact-full"),
                ent,
                args.User);
            return;
        }

        args.Handled = true;
        _hands.TryDropIntoContainer(args.User, args.Used, ent.Comp.Storage);
        UpdateUi(ent.Owner);
    }

    private void OnBreak(Entity<WorkshopComponent> ent, ref BreakageEventArgs args)
    {
        _container.EmptyContainer(ent.Comp.Storage);
    }

    private void OnAddToQueue(Entity<WorkshopComponent> ent, ref WorkshopAddToQueueMessage args)
    {
        AddToQueue(new(ent, ent.Comp), args.ProtoId);
    }

    private void OnRemoveFromQueue(Entity<WorkshopComponent> ent, ref WorkshopRemoveFromQueueMessage args)
    {
        RemoveFromQueue(new(ent, ent.Comp), args.Index);
    }

    #endregion

    private void DeleteIngredients(WorkshopComponent comp, WorkshopRecipePrototype proto)
    {
        var ingredients = new WorkshopRecipeIngredients(proto.Ingredients);

        foreach (var uid in comp.Storage.ContainedEntities)
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

            if (!_solution.TryGetDrainableSolution(uid, out var solutionEntity, out var solution))
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

                _solution.RemoveReagent(solutionEntity.Value, reagent, quant);
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

        foreach (var uid in comp.Storage.ContainedEntities)
        {
            if (Prototype(uid) is { } proto && !items.TryAdd(proto, 1))
                items[proto]++;

            if (_stackQuery.TryComp(uid, out var stack) && !materials.TryAdd(stack.StackTypeId, stack.Count))
                materials[stack.StackTypeId] += stack.Count;

            if (!_solution.TryGetDrainableSolution(uid, out _, out var sol))
                continue;

            foreach (var (reagent, quantity) in sol)
            {
                if (!reagents.TryAdd(reagent.Prototype, quantity))
                    reagents[reagent.Prototype] += quantity;
            }
        }
    }

    private WorkshopRecipeIngredients GetRemainingIngredients(
        WorkshopComponent comp,
        ProtoId<WorkshopRecipePrototype> protoId)
    {
        if (!_proto.Resolve(protoId, out var proto))
            return new();

        var ingredients = new WorkshopRecipeIngredients(proto.Ingredients);

        foreach (var uid in comp.Storage.ContainedEntities)
        {
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

            if (!_solution.TryGetDrainableSolution(uid, out _, out var solution))
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

    private void UpdateAppearance(Entity<WorkshopComponent?, WorkshopVisualsComponent?, AppearanceComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp1, ref ent.Comp2, ref ent.Comp3, false)
            || ent.Comp2.ItemsVisualStates.Count == 0)
            return;

        var state = ent.Comp2.ItemsVisualStates
            .Where(x => x.Value <= ent.Comp1.Storage.Count)
            .OrderBy(x => x.Value)
            .LastOrDefault()
            .Key ?? ent.Comp2.ItemsVisualStates.First().Key;

        _appearance.SetData(ent, WorkshopVisuals.Items, state, ent.Comp3);
        _appearance.SetData(
            ent,
            WorkshopVisuals.Crafting,
            ent.Comp1.CraftEndTime == null ? ent.Comp2.IdleState : state,
            ent.Comp3);
    }

    private void UpdateUi(Entity<WorkshopComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        _ui.SetUiState(ent.Owner,
            WorkshopUiKey.Key,
            new WorkshopUiState(
                GetNetEntityArray(ent.Comp.Storage.ContainedEntities.ToArray()),
                ent.Comp.Queue,
                ent.Comp.CraftEndTime,
                ent.Comp.MaxQueue,
                GetNetEntity(ent.Comp.User)));
    }

    private void ContinueCrafting(Entity<WorkshopComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp)
            || ent.Comp.Queue.FirstOrNull() is not { } current)
            return;

        if (current.Pathfinding.Count == 0)
        {
            RemoveFromQueue(ent, 0);
            return;
        }

        current.Pathfinding.RemoveAt(0);

        if (_proto.Resolve(GetQueueRecipe(ent.Comp, 0), out var proto))
        {
            if (TryComp(ent.Comp.User, out HTNComponent? htn))
                htn.Blackboard.SetValue(ent.Comp.TargetRecipeKey, proto);

            ent.Comp.CraftEndTime = _timing.CurTime + proto.CraftingTime;
        }
        else
        {
            if (ent.Comp.User != null)
                _npcControl.FinishTask(ent.Comp.User.Value);

            ent.Comp.User = null;
            ent.Comp.CraftEndTime = null;
            ent.Comp.PlayingStream = _audio.Stop(ent.Comp.PlayingStream);
            _audio.PlayPvs(ent.Comp.CraftingDoneSound, ent);
        }

        UpdateAppearance(ent.Owner);
        UpdateUi(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var enumerator = EntityQueryEnumerator<WorkshopComponent, TransformComponent>();
        while (enumerator.MoveNext(out var uid, out var comp, out var xform))
        {
            if (comp.CraftEndTime == null || comp.CraftEndTime > _timing.CurTime)
                continue;

            if (GetQueueRecipe(comp, 0) is not { } protoId || !_proto.Resolve(protoId, out var proto))
            {
                comp.CraftEndTime = null;
                comp.User = null;
                UpdateAppearance(uid);
                UpdateUi(uid);
                comp.PlayingStream = _audio.Stop(comp.PlayingStream);
                _audio.PlayPvs(comp.CraftingDoneSound, uid);
                continue;
            }

            foreach (var exp in proto.SkillsUp)
            {
                _skills.AddExperience(uid, exp);
            }

            _audio.PlayPvs(comp.CraftingDoneSound, uid);
            DeleteIngredients(comp, proto);
            Spawn(proto.Result, xform.Coordinates);
            ContinueCrafting(new(uid, comp));
            UpdateAppearance(uid);
        }
    }

    #region API

    /// <summary>
    /// Checks if all the ingredients for the target recipe are available in the workshop.
    /// </summary>
    [PublicAPI, Pure]
    public bool CanCraft(Entity<WorkshopComponent?> ent, ProtoId<WorkshopRecipePrototype> protoId)
    {
        if (!Resolve(ent, ref ent.Comp)
            || !_proto.Resolve(protoId, out var proto)
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

        var recipe = comp.Queue[index];
        return recipe.Pathfinding.Count > 0 ? recipe.Pathfinding[0] : recipe.Recipe;
    }

    /// <summary>
    /// Builds a path from recipes to create the target recipe.
    /// </summary>
    /// <param name="protoId">Target recipe.</param>
    /// <returns>A recipe path.</returns>
    [PublicAPI, Pure]
    public List<ProtoId<WorkshopRecipePrototype>> GetRecipePath(ProtoId<WorkshopRecipePrototype> protoId)
    {
        var path = new List<ProtoId<WorkshopRecipePrototype>>();

        var recipes = new List<Dictionary<ProtoId<WorkshopRecipePrototype>, int>>();
        var queue = new Queue<(int Depth, ProtoId<WorkshopRecipePrototype> Proto, int Quantity)>();

        queue.Enqueue((0, protoId, 1));

        while (queue.TryDequeue(out var recipe))
        {
            if (!_proto.Resolve(recipe.Proto, out var proto))
                continue;

            if (recipes.Count <= recipe.Depth)
                recipes.Add(new());

            // If several identical ingredients are used at the same depth, we add their quantities together
            if (!recipes[recipe.Depth].TryAdd(recipe.Proto, recipe.Quantity))
                recipes[recipe.Depth][recipe.Proto] += recipe.Quantity;

            foreach (var (ent, q) in proto.Ingredients.Items)
            {
                // Else, search for a recipe for the ingredient
                if (_recipes.TryGetValue(ent, out var list))
                    queue.Enqueue((recipe.Depth + 1, _random.Pick(list), q * recipe.Quantity));
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
            || GetQueueRecipe(ent.Comp, 0) is not { } protoId
            || !CanCraft(ent, protoId)
            || !_proto.Resolve(protoId, out var proto))
            return false;

        _audio.PlayPvs(ent.Comp.StartCraftingSound, ent);
        ent.Comp.PlayingStream =
            _audio.PlayPvs(ent.Comp.LoopingSound, ent, AudioParams.Default.WithLoop(true).WithMaxDistance(5))?.Entity;
        ent.Comp.CraftEndTime = _timing.CurTime + proto.CraftingTime;
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

        ent.Comp.Queue.Add((protoId, GetRecipePath(protoId)));

        // Creating a target for a passive task
        if (!HasComp<PassiveNpcTaskTargetComponent>(ent))
        {
            foreach (var owner in _ownership.GetOwners(ent))
            {
                if (!HasComp<NpcControlComponent>(owner))
                    continue;

                _npcControl.SetPassiveTaskTarget(
                    owner,
                    ent.Comp.Task,
                    ent,
                    removeWhenFailed: false,
                    additionalKeys: new() { {ent.Comp.TargetRecipeKey, protoId} });
                break;
            }
        }

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

        // If the last recipe is deleted, delete the target of the passive task
        if (ent.Comp.Queue.Count == 0)
        {
            if (ent.Comp.User != null)
                _npcControl.FinishTask(ent.Comp.User.Value);
            else
                RemComp<PassiveNpcTaskTargetComponent>(ent);

            ent.Comp.User = null;
        }
        // If the first recipe is deleted, we replace the target of the passive task
        else if (index == 0)
        {
            if (CanCraft(ent, ent.Comp.Queue[0].Recipe)
                && _proto.Resolve(ent.Comp.Queue[0].Recipe, out var proto))
            {
                if (TryComp(ent.Comp.User, out HTNComponent? htn))
                    htn.Blackboard.SetValue(ent.Comp.TargetRecipeKey, ent.Comp.Queue[0]);

                ent.Comp.CraftEndTime = _timing.CurTime + proto.CraftingTime;
            }
            else
            {
                if (ent.Comp.User != null)
                    _npcControl.FinishTask(ent.Comp.User.Value);

                ent.Comp.User = null;
                ent.Comp.CraftEndTime = null;
                ent.Comp.PlayingStream = _audio.Stop(ent.Comp.PlayingStream);
                _audio.PlayPvs(ent.Comp.CraftingDoneSound, ent);
            }
        }

        UpdateAppearance(ent);
        UpdateUi(ent);
        return true;
    }

    /// <summary>
    /// Returns all recipes from the recipes table.
    /// </summary>
    [PublicAPI, Pure]
    public HashSet<ProtoId<WorkshopRecipePrototype>> GetTableRecipes(ProtoId<WorkshopRecipeTablePrototype> protoId)
    {
        var tableRecipes = new HashSet<ProtoId<WorkshopRecipePrototype>>();

        if (!_proto.Resolve(protoId, out var proto))
            return tableRecipes;

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
        if (!_proto.Resolve(protoId, out var proto))
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

    #endregion
}
