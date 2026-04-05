using Content.Shared._RF.Workshops.Prototypes;
using Content.Shared._RF.Workshops.Systems;
using Content.Shared.Item;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._RF.Workshops.Components;

[Access(typeof(SharedWorkshopSystem))]
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
public sealed partial class WorkshopComponent : Component
{
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
    [AutoNetworkedField, ViewVariables]
    public List<WorkshopQueueEntry> Queue = new();

    /// <summary>
    /// The maximum number of recipes that can be in the queue.
    /// </summary>
    [DataField, ViewVariables]
    public int MaxQueue = 10;

    /// <summary>
    /// A user who is currently crafting.
    /// </summary>
    [AutoNetworkedField, ViewVariables]
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
    [AutoNetworkedField, ViewVariables]
    public TimeSpan? CraftEndTime;

    /// <summary>
    /// When the crafting started.
    /// </summary>
    [AutoNetworkedField, ViewVariables]
    public TimeSpan? CraftStartTime;

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

[Serializable, NetSerializable]
public readonly record struct WorkshopQueueEntry(
    ProtoId<WorkshopRecipePrototype> Recipe,
    ProtoId<WorkshopRecipePrototype>[] Pathfinding)
{
    public ProtoId<WorkshopRecipePrototype> Current
        => Pathfinding.Length > 0 ? Pathfinding[0] : Recipe;

    public WorkshopQueueEntry Advance()
        => Pathfinding.Length == 0 ? this : this with { Pathfinding = Pathfinding[1..] };
}

[Serializable, NetSerializable]
public sealed class WorkshopAddToQueueMessage(ProtoId<WorkshopRecipePrototype> protoId) : BoundUserInterfaceMessage
{
    public ProtoId<WorkshopRecipePrototype> ProtoId = protoId;
}

[Serializable, NetSerializable]
public sealed class WorkshopRemoveFromQueueMessage(int index) : BoundUserInterfaceMessage
{
    public int Index = index;
}

[Serializable, NetSerializable]
public sealed class WorkshopUiState(
    NetEntity[] contained,
    NetEntity[] containedResults,
    int resultsCapacity,
    List<WorkshopQueueEntry> queue,
    TimeSpan? craftEndTime,
    TimeSpan? craftStartTime,
    int maxQueue,
    NetEntity? user,
    ProtoId<WorkshopRecipeTablePrototype> recipesTable) : BoundUserInterfaceState
{
    public NetEntity[] Contained = contained;
    public NetEntity[] ContainedResults = containedResults;
    public int ResultsCapacity = resultsCapacity;
    public List<WorkshopQueueEntry> Queue = queue;
    public TimeSpan? CraftEndTime = craftEndTime;
    public TimeSpan? CraftStartTime = craftStartTime;
    public int MaxQueue = maxQueue;
    public NetEntity? User = user;
    public ProtoId<WorkshopRecipeTablePrototype> RecipesTable = recipesTable;
}

[Serializable, NetSerializable]
public enum WorkshopUiKey : byte
{
    Key,
}
