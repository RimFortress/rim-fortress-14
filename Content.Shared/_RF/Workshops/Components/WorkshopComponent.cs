using System.Numerics;
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
    [DataField, AutoNetworkedField]
    public WorkshopQueue Queue = new();

    /// <summary>
    /// The maximum number of recipes that can be in the queue.
    /// </summary>
    [DataField]
    public int MaxQueue = 10;

    /// <summary>
    /// A modifier for the recipe crafting speed in the workshop.
    /// </summary>
    [DataField]
    public float CraftingTimeModifier = 1.0f;

    /// <summary>
    /// The coordinates of the location relative to the workshop where the NPCs will be working.
    /// </summary>
    [DataField]
    public Vector2 CraftingPlace = Vector2.Zero;

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
    [DataField]
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
    [DataField]
    public ProtoId<ItemSizePrototype> MaxItemSize = "Normal";

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
    [ViewVariables]
    public EntityUid? PlayingStream;

    public bool Crafting => Queue.Crafting;
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
public sealed class WorkshopRepeatMessage(int index) : BoundUserInterfaceMessage
{
    public int Index = index;
}

[Serializable, NetSerializable]
public sealed class WorkshopSuspendMessage(int index) : BoundUserInterfaceMessage
{
    public int Index = index;
}

[Serializable, NetSerializable]
public sealed class WorkshopSuppliedStockMessage(NetEntity stockId) : BoundUserInterfaceMessage
{
    public NetEntity StockId = stockId;
}

[Serializable, NetSerializable]
public sealed class WorkshopUiState(
    NetEntity[] contained,
    NetEntity[] containedResults,
    int resultsCapacity,
    WorkshopQueue queue,
    int maxQueue,
    NetEntity? user,
    ProtoId<WorkshopRecipeTablePrototype> recipesTable) : BoundUserInterfaceState
{
    public NetEntity[] Contained = contained;
    public NetEntity[] ContainedResults = containedResults;
    public int ResultsCapacity = resultsCapacity;
    public WorkshopQueue Queue = queue;
    public int MaxQueue = maxQueue;
    public NetEntity? User = user;
    public ProtoId<WorkshopRecipeTablePrototype> RecipesTable = recipesTable;
}

[Serializable, NetSerializable]
public enum WorkshopUiKey : byte
{
    Key,
}
