using Content.Shared._RF.Workshops.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._RF.Workshops.Components;

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
    List<(ProtoId<WorkshopRecipePrototype>, List<ProtoId<WorkshopRecipePrototype>>)> queue,
    TimeSpan? craftEndTime,
    int maxQueue,
    NetEntity? user) : BoundUserInterfaceState
{
    public NetEntity[] Contained = contained;
    public NetEntity[] ContainedResults = containedResults;
    public int ResultsCapacity = resultsCapacity;
    public List<(ProtoId<WorkshopRecipePrototype> Recipe, List<ProtoId<WorkshopRecipePrototype>> Pathfinding)> Queue = queue;
    public TimeSpan? CraftEndTime = craftEndTime;
    public int MaxQueue = maxQueue;
    public NetEntity? User = user;
}

[Serializable, NetSerializable]
public enum WorkshopUiKey : byte
{
    Key,
}
