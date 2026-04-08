using Content.Shared._RF.Workshops.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._RF.Workshops;

[DataDefinition, Serializable, NetSerializable]
public sealed partial class WorkshopQueue
{
    /// <summary>
    /// All entries currently present in the workshop queue.
    /// </summary>
    [DataField]
    public List<WorkshopQueueEntry> Queue = new();

    /// <summary>
    /// Index of the currently selected entry in <see cref="Queue"/>.
    /// </summary>
    [DataField]
    public int Index;

    /// <summary>
    /// Number of entries in the queue.
    /// </summary>
    public int Count => Queue.Count;

    /// <summary>
    /// The currently selected queue entry, or <c>null</c> if the queue is empty.
    /// </summary>
    public WorkshopQueueEntry? Entry => Count == 0 ? null : Queue[NormalizeIndex(Index)];

    /// <summary>
    /// The recipe currently targeted by the active queue entry.
    /// </summary>
    public ProtoId<WorkshopRecipePrototype>? Recipe => Entry?.Current;

    /// <summary>
    /// Returns <c>true</c> when the active queue entry is actively crafting.
    /// </summary>
    public bool Crafting => Entry is { CraftingEndTime: not null, Suspended: false };

    /// <summary>
    /// Advances the queue by one logical step.
    /// If the current entry still has path steps remaining, advances its path.
    /// If the current recipe is finished, removes it unless it is repeatable.
    /// Repeatable recipes are reset and moved to the next runnable entry.
    /// </summary>
    public void Advance()
    {
        if (Count == 0)
        {
            Index = 0;
            return;
        }

        Index = NormalizeIndex(Index);

        var entry = Queue[Index];

        if (!entry.PathFinished)
        {
            entry.AdvancePath();
            return;
        }

        if (!entry.Repeat)
        {
            Queue.RemoveAt(Index);
            Index = ValidateIndex(Index);
            return;
        }

        entry.ResetPath();

        Index = ValidateIndex(Index + 1);
    }

    /// <summary>
    /// Stops execution of the currently selected entry without removing it from the queue.
    /// </summary>
    public void StopCurrent()
    {
        if (Entry != null)
            Queue[Index].CraftingEndTime = null;
    }

    /// <summary>
    /// Sets the suspended flag on the entry at the specified index.
    /// Suspended entries are skipped by queue navigation.
    /// </summary>
    public void SetSuspended(int index, bool suspended)
    {
        if (index < 0 || index >= Count)
            return;

        Queue[index].Suspended = suspended;
        Index = ValidateIndex(Index);
    }

    public void SetRepeat(int index, bool repeat)
    {
        if (index > 0 && index < Count)
            Queue[index].Repeat = repeat;
    }

    /// <summary>
    /// Removes the entry at the specified index and keeps the queue index valid.
    /// </summary>
    public void RemoveAt(int index)
    {
        if (index < 0 || index >= Count)
            return;

        Queue.RemoveAt(index);

        if (Count == 0)
        {
            Index = 0;
            return;
        }

        if (index < Index)
            Index--;
        else if (index == Index)
            Index = ValidateIndex(Index);

        Index = NormalizeIndex(Index);
    }

    public void Add(ProtoId<WorkshopRecipePrototype> recipe, ProtoId<WorkshopRecipePrototype>[] pathfinding)
    {
        Queue.Add(new WorkshopQueueEntry { Recipe = recipe, Pathfinding = pathfinding });
    }

    public void SetEndTime(TimeSpan? time)
    {
        if (Entry is { } entry)
            entry.CraftingEndTime = time;
    }

    /// <summary>
    /// Returns the current entry index wrapped into the valid queue range.
    /// </summary>
    private int NormalizeIndex(int index)
    {
        if (Count == 0)
            return 0;

        index %= Count;
        if (index < 0)
            index += Count;

        return index;
    }

    /// <summary>
    /// Finds the next non-suspended entry starting from <paramref name="index"/>.
    /// If all entries are suspended, returns 0.
    /// </summary>
    private int ValidateIndex(int index)
    {
        if (Count == 0)
            return 0;

        index = NormalizeIndex(index);

        for (var i = 0; i < Count; i++)
        {
            var candidate = (index + i) % Count;
            if (!Queue[candidate].Suspended)
                return candidate;
        }

        return 0;
    }
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial class WorkshopQueueEntry
{
    /// <summary>
    /// Root recipe stored in the queue entry.
    /// </summary>
    [DataField]
    public ProtoId<WorkshopRecipePrototype> Recipe;

    /// <summary>
    /// Path of intermediate recipes used to reach <see cref="Recipe"/>.
    /// </summary>
    [DataField]
    public ProtoId<WorkshopRecipePrototype>[] Pathfinding = Array.Empty<ProtoId<WorkshopRecipePrototype>>();

    /// <summary>
    /// When the currently active recipe finishes.
    /// <c>null</c> means the entry is paused or not currently crafting.
    /// </summary>
    [DataField]
    public TimeSpan? CraftingEndTime;

    /// <summary>
    /// Current position inside <see cref="Pathfinding"/>.
    /// </summary>
    [DataField]
    public int PathIndex;

    /// <summary>
    /// If true, the entry is reinserted into the cycle after completion.
    /// </summary>
    [DataField]
    public bool Repeat;

    /// <summary>
    /// If true, the entry is skipped by queue navigation until resumed.
    /// </summary>
    [DataField]
    public bool Suspended;

    /// <summary>
    /// The recipe currently being processed by this entry.
    /// If the path is exhausted, this returns <see cref="Recipe"/>.
    /// </summary>
    public ProtoId<WorkshopRecipePrototype> Current
        => HasPath && PathIndex < Pathfinding.Length ? Pathfinding[PathIndex] : Recipe;

    /// <summary>
    /// Returns <c>true</c> when a path exists.
    /// </summary>
    public bool HasPath => Pathfinding.Length > 0;

    /// <summary>
    /// Returns <c>true</c> when the path has been fully consumed.
    /// </summary>
    public bool PathFinished => !HasPath || PathIndex >= Pathfinding.Length - 1;

    /// <summary>
    /// Advances the path position by one step, clamped to the last valid step.
    /// </summary>
    public void AdvancePath()
    {
        if (HasPath && PathIndex < Pathfinding.Length - 1)
            PathIndex++;
    }

    /// <summary>
    /// Resets the path position to the first step.
    /// </summary>
    public void ResetPath() => PathIndex = 0;

    /// <summary>
    /// Returns the remaining path steps from the current position onward.
    /// </summary>
    public ProtoId<WorkshopRecipePrototype>[] RemainingPath
        => HasPath && PathIndex < Pathfinding.Length
            ? Pathfinding[PathIndex..]
            : Array.Empty<ProtoId<WorkshopRecipePrototype>>();
}
