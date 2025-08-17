namespace Content.Shared._RF.NPC;

public abstract partial class SharedRoutineNpcTasksComponent : Component
{
    /// <summary>
    /// Maximum possible priority for tasks
    /// </summary>
    [DataField, ViewVariables]
    public int MaxPriority = 10;

    /// <summary>
    /// Dictionary containing job ids and their priorities
    /// </summary>
    /// <remarks>
    /// job id, priority
    /// </remarks>
    [ViewVariables]
    public Dictionary<int, int> Jobs = new();
}
