using Content.Server._RF.NPC.Prototypes;
using Content.Server._RF.Workshops.Systems;
using Content.Shared._RF.Workshops.Prototypes;
using Content.Shared.Item;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.Workshops.Components;

[RegisterComponent, Access(typeof(WorkshopSystem))]
public sealed partial class WorkshopComponent : Component
{
    /// <summary>
    /// The task that will be given for crafting the recipe.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<NpcTaskPrototype> Task;

    /// <summary>
    /// The key to which the target recipe will be saved in Blackboard when production begins.
    /// </summary>
    [DataField]
    public string TargetRecipeKey = "TargetRecipe";

    /// <summary>
    /// Entity that will be spawned when crafting fails.
    /// </summary>
    [DataField]
    public EntProtoId? CraftingFailResult;

    /// <summary>
    /// Table of all recipes available in this workshop.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<WorkshopRecipeTablePrototype> Recipes;

    /// <summary>
    /// Current production queue.
    /// </summary>
    [ViewVariables]
    public readonly List<(ProtoId<WorkshopRecipePrototype> Recipe, List<ProtoId<WorkshopRecipePrototype>> Pathfinding)> Queue = new();

    /// <summary>
    /// The maximum number of recipes that can be in the queue.
    /// </summary>
    [DataField, ViewVariables]
    public int MaxQueue = 10;

    /// <summary>
    /// A user who is currently crafting.
    /// </summary>
    [DataField, ViewVariables]
    public EntityUid? User;

    /// <summary>
    /// A container that holds the entities used to craft recipes.
    /// </summary>
    [ViewVariables]
    public Container ContentStorage = default!;

    /// <summary>
    /// Workshop container ID.
    /// </summary>
    [DataField]
    public string ContentContainerId = "workshop_entity_container";

    /// <summary>
    /// The maximum number of items that can be stored in the workshop.
    /// </summary>
    [DataField, ViewVariables]
    public int ContentCapacity = 12;

    /// <summary>
    /// A container that stores the crafting results.
    /// </summary>
    [ViewVariables]
    public Container ResultStorage = default!;

    /// <summary>
    /// Workshop crafting results container ID.
    /// </summary>
    [DataField]
    public string ResultContainerId = "workshop_result_container";

    /// <summary>
    /// The maximum number of items that can be stored in the crafting results container.
    /// </summary>
    [DataField, ViewVariables]
    public int ResultCapacity = 10;

    /// <summary>
    /// The maximum size of an item that can be stored in the workshop.
    /// </summary>
    [DataField, ViewVariables]
    public ProtoId<ItemSizePrototype> MaxItemSize = "Normal";

    /// <summary>
    /// When the current recipe is finished.
    /// </summary>
    [DataField]
    public TimeSpan? CraftEndTime;

    /// <summary>
    /// A sound that plays when crafting begins in the workshop.
    /// </summary>
    [DataField]
    public SoundSpecifier? StartCraftingSound;

    /// <summary>
    /// A sound that plays when the workshop crafting is complete.
    /// </summary>
    [DataField]
    public SoundSpecifier? CraftingDoneSound;

    /// <summary>
    /// A sound that plays when the workshop crafting fails.
    /// </summary>
    [DataField]
    public SoundSpecifier? CraftingFailSound;

    /// <summary>
    /// A looping sound that plays while crafting in the workshop.
    /// </summary>
    [DataField]
    public SoundSpecifier? LoopingSound;

    /// <summary>
    /// Looping sound stream.
    /// </summary>
    public EntityUid? PlayingStream;
}
